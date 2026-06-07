#!/bin/bash
# =============================================================================
# backup.sh
#
# On-demand, ZERO-DOWNTIME backup of the PayrollDB database using SQL Server's
# native BACKUP DATABASE command. Produces a .bak file that can be restored
# to any SQL Server 2016+ instance.
#
# Behaviour:
#   1. Verifies the payroll-sql container is running (errors out cleanly if not)
#   2. Issues BACKUP DATABASE inside the container — SQL Server coordinates a
#      transactionally-consistent snapshot while staying online for queries
#   3. Copies the .bak file out of the container to ~/db-backups/PayrollDB/
#   4. Verifies the .bak file with RESTORE VERIFYONLY (proves it's restorable)
#   5. Prunes older backups, keeping only the most recent BACKUP_KEEP_COUNT
#
# Usage:
#   ./backup.sh                  # take a backup right now
#   ./backup.sh --label hotfix    # take a backup tagged "hotfix" in the filename
#
# Trade-offs vs dev-down.sh's filesystem backup:
#   - backup.sh:    online, .bak format, portable across servers, restorable to
#                   any instance, smaller files (log compressed)
#   - dev-down.sh:  offline, raw volume contents, only restorable to a
#                   Docker volume layout, larger (every system file copied)
#
# Both are useful: this for live snapshots and named save-points,
# dev-down.sh for full-environment backups at clean shutdown.
# =============================================================================

set -euo pipefail

# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------
readonly CONTAINER_NAME="payroll-sql"
readonly DB_NAME="PayrollDB"
readonly SA_PASSWORD="YourStrong!Pass123"
readonly SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

readonly CONTAINER_BACKUP_DIR="/var/opt/mssql/backups"
readonly HOST_BACKUP_DIR="${HOME}/docker-backups/${DB_NAME}"
readonly BACKUP_KEEP_COUNT=10
readonly TIMESTAMP="$(date +%Y%m%d-%H%M%S)"

# Parse optional label from --label flag
LABEL=""
if [[ "${1:-}" == "--label" && -n "${2:-}" ]]; then
    # Sanitize: only allow alphanumeric, dash, underscore
    LABEL="$(echo "$2" | tr -cd '[:alnum:]_-')"
fi

# Build filename: with-or-without optional label segment
if [[ -n "${LABEL}" ]]; then
    BACKUP_FILENAME="${DB_NAME}_${TIMESTAMP}_${LABEL}.bak"
else
    BACKUP_FILENAME="${DB_NAME}_${TIMESTAMP}.bak"
fi

readonly CONTAINER_BACKUP_PATH="${CONTAINER_BACKUP_DIR}/${BACKUP_FILENAME}"
readonly HOST_BACKUP_PATH="${HOST_BACKUP_DIR}/${BACKUP_FILENAME}"

# Color codes
readonly C_GREEN='\033[0;32m'
readonly C_YELLOW='\033[1;33m'
readonly C_RED='\033[0;31m'
readonly C_CYAN='\033[0;36m'
readonly C_DIM='\033[2m'
readonly C_RESET='\033[0m'

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
log_info()    { echo -e "${C_CYAN}ℹ${C_RESET}  $*"; }
log_success() { echo -e "${C_GREEN}✅${C_RESET} $*"; }
log_warn()    { echo -e "${C_YELLOW}⚠️${C_RESET}  $*"; }
log_error()   { echo -e "${C_RED}❌${C_RESET} $*" >&2; }

# Run a sqlcmd query inside the container. Quietly fails -> caller handles.
sql_exec() {
    local query="$1"
    docker exec "${CONTAINER_NAME}" "${SQLCMD}" \
        -S localhost -U sa -P "${SA_PASSWORD}" -C \
        -Q "${query}"
}

# Run sqlcmd in silent mode — used for verification queries
sql_exec_silent() {
    local query="$1"
    docker exec "${CONTAINER_NAME}" "${SQLCMD}" \
        -S localhost -U sa -P "${SA_PASSWORD}" -C \
        -h -1 -W -Q "${query}" 2>/dev/null
}

cleanup_container_temp() {
    # Best-effort: remove the temp .bak from the container after host copy.
    docker exec "${CONTAINER_NAME}" rm -f "${CONTAINER_BACKUP_PATH}" 2>/dev/null || true
}

# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------
echo ""
echo -e "${C_CYAN}═══════════════════════════════════════════════════════${C_RESET}"
echo -e "${C_CYAN}  backup.sh — Online Backup of ${DB_NAME}${C_RESET}"
echo -e "${C_CYAN}═══════════════════════════════════════════════════════${C_RESET}"

# -----------------------------------------------------------------------------
# Step 1 — Container must be running
# -----------------------------------------------------------------------------
log_info "Step 1/5: Checking container status..."
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    log_error "Container '${CONTAINER_NAME}' is not running."
    log_error "Start it with: ./dev-up.sh"
    log_error "Or if you want a full-volume backup at shutdown, use: ./dev-down.sh"
    exit 1
fi
log_success "Container is running"
echo ""

# -----------------------------------------------------------------------------
# Step 2 — Database must exist and be online
# -----------------------------------------------------------------------------
log_info "Step 2/5: Verifying database is accessible..."
DB_STATE="$(sql_exec_silent "SET NOCOUNT ON; SELECT state_desc FROM sys.databases WHERE name = '${DB_NAME}'" | tr -d '[:space:]\r' | head -1)"
if [[ -z "${DB_STATE}" ]]; then
    log_error "Database '${DB_NAME}' does not exist on this instance."
    log_error "Apply the schema first: see README.md 'Apply schema and seed data' section."
    exit 1
fi
if [[ "${DB_STATE}" != "ONLINE" ]]; then
    log_error "Database '${DB_NAME}' is in state '${DB_STATE}' — cannot back up."
    log_error "Investigate before proceeding."
    exit 1
fi
log_success "Database '${DB_NAME}' is ONLINE and ready"
echo ""

# -----------------------------------------------------------------------------
# Step 3 — Take the backup (online, zero downtime)
# -----------------------------------------------------------------------------
log_info "Step 3/5: Running BACKUP DATABASE inside the container..."
log_info "Target inside container: ${CONTAINER_BACKUP_PATH}"

# Ensure the in-container backup directory exists
docker exec "${CONTAINER_NAME}" mkdir -p "${CONTAINER_BACKUP_DIR}" 2>/dev/null

# BACKUP DATABASE options used:
#   - COMPRESSION:     reduces .bak file size, faster IO
#   - CHECKSUM:        SQL Server computes a checksum and validates integrity during the backup
#   - INIT, FORMAT:    overwrite if a file with the same name exists (unique here, but defensive)
#   - STATS = 10:      print progress every 10% (useful for larger DBs; quick for ours)
#   - NAME:            human-readable label embedded in the backup metadata
BACKUP_QUERY="
BACKUP DATABASE [${DB_NAME}]
TO DISK = N'${CONTAINER_BACKUP_PATH}'
WITH
    COMPRESSION,
    CHECKSUM,
    INIT,
    FORMAT,
    STATS = 10,
    NAME = N'On-demand backup ${TIMESTAMP}';
"

if sql_exec "${BACKUP_QUERY}"; then
    log_success "Backup written inside container"
else
    log_error "BACKUP DATABASE failed — see SQL output above"
    cleanup_container_temp
    exit 1
fi
echo ""

# -----------------------------------------------------------------------------
# Step 4 — Verify the .bak file (RESTORE VERIFYONLY)
# -----------------------------------------------------------------------------
log_info "Step 4/5: Verifying backup integrity (RESTORE VERIFYONLY)..."

VERIFY_QUERY="
RESTORE VERIFYONLY
FROM DISK = N'${CONTAINER_BACKUP_PATH}'
WITH CHECKSUM;
"

if sql_exec "${VERIFY_QUERY}"; then
    log_success "Backup verified — confirmed restorable"
else
    log_error "Backup verification FAILED — the .bak is corrupted or unreadable"
    log_error "NOT copying to host. The bad backup will be cleaned up automatically."
    cleanup_container_temp
    exit 1
fi
echo ""

# -----------------------------------------------------------------------------
# Step 5 — Copy .bak from container to host and prune old backups
# -----------------------------------------------------------------------------
log_info "Step 5/5: Copying backup to host and pruning history..."

mkdir -p "${HOST_BACKUP_DIR}"

if docker cp "${CONTAINER_NAME}:${CONTAINER_BACKUP_PATH}" "${HOST_BACKUP_PATH}"; then
    BACKUP_SIZE="$(du -sh "${HOST_BACKUP_PATH}" | cut -f1)"
    log_success "Copied to host: ${HOST_BACKUP_PATH} (${BACKUP_SIZE})"
else
    log_error "docker cp failed — the backup remains inside the container at ${CONTAINER_BACKUP_PATH}"
    exit 1
fi

# Remove the temp copy inside the container so it doesn't accumulate there
cleanup_container_temp

# Prune: keep only the most recent BACKUP_KEEP_COUNT files
echo ""
log_info "Pruning old backups (keeping the last ${BACKUP_KEEP_COUNT})..."

mapfile -t ALL_BACKUPS < <(
    find "${HOST_BACKUP_DIR}" -mindepth 1 -maxdepth 1 -type f -name '*.bak' \
        -printf '%f\n' 2>/dev/null | sort -r
)
TOTAL_COUNT="${#ALL_BACKUPS[@]}"

if (( TOTAL_COUNT <= BACKUP_KEEP_COUNT )); then
    log_info "Currently have ${TOTAL_COUNT} backup(s) — none to prune."
else
    PRUNE_COUNT=$((TOTAL_COUNT - BACKUP_KEEP_COUNT))
    log_info "Currently have ${TOTAL_COUNT} backup(s) — removing the oldest ${PRUNE_COUNT}..."
    for (( i = BACKUP_KEEP_COUNT; i < TOTAL_COUNT; i++ )); do
        OLD_FILE="${ALL_BACKUPS[$i]}"
        echo -e "  ${C_DIM}removing  ${OLD_FILE}${C_RESET}"
        rm -f "${HOST_BACKUP_DIR}/${OLD_FILE}"
    done
fi
echo ""

# -----------------------------------------------------------------------------
# Final summary
# -----------------------------------------------------------------------------
log_success "Backup complete!"
echo ""
echo -e "${C_DIM}Current backups in ${HOST_BACKUP_DIR}:${C_RESET}"
find "${HOST_BACKUP_DIR}" -mindepth 1 -maxdepth 1 -type f -name '*.bak' \
    -printf '%f\n' 2>/dev/null \
    | sort -r \
    | while read -r FILE; do
        SIZE="$(du -sh "${HOST_BACKUP_DIR}/${FILE}" | cut -f1)"
        echo -e "  ${C_DIM}•${C_RESET} ${FILE}  ${C_DIM}(${SIZE})${C_RESET}"
    done

TOTAL_SIZE="$(du -sh "${HOST_BACKUP_DIR}" 2>/dev/null | cut -f1)"
echo ""
echo -e "${C_DIM}Total backup footprint: ${TOTAL_SIZE}${C_RESET}"
echo ""
echo -e "${C_DIM}To restore from a backup, see the 'Restore' section in README.md${C_RESET}"
echo ""