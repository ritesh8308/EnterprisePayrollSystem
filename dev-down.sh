#!/bin/bash
# =============================================================================
# dev-down.sh
#
# Gracefully stops the SQL Server container AND takes a backup snapshot of the
# Docker volume that holds the PayrollDB database files.
#
# Behaviour:
#   1. Stops the container via `docker compose down` (graceful — SQL Server
#      finishes flushing to disk before exit).
#   2. rsyncs the now-quiesced volume directory into ~/docker-backups/<ts>/.
#   3. Prunes backups older than the most recent BACKUP_KEEP_COUNT (default: 5).
#
# Rationale: today's apt-purge incident wiped /var/lib/docker/volumes/. Although
# the database state is reproducible from Database/*.sql, point-in-time backups
# preserve any ad-hoc test data inserted during a session (smoke tests,
# manually inserted rows, payroll experiments) that the seed scripts wouldn't
# restore.
#
# Safety:
#   - The backup runs AFTER the container stops so data is consistent on disk.
#   - Uses rsync incremental copy (fast after first run, ~MB of changed blocks).
#   - Old backups are listed before deletion (no silent purges).
#   - Script aborts on any error; existing backups are never touched on failure.
# =============================================================================

set -euo pipefail   # Fail fast: stop on error, undefined var, or pipe failure

# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------
readonly PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
readonly VOLUME_NAME="enterprisepayrollsystem_sqldata"
readonly DOCKER_VOLUMES_DIR="/var/lib/docker/volumes"
readonly BACKUP_ROOT="${HOME}/docker-backups/${VOLUME_NAME}"
readonly BACKUP_KEEP_COUNT=5         # Retain only the last N backups
readonly TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
readonly BACKUP_TARGET="${BACKUP_ROOT}/${TIMESTAMP}"

# Color codes for readable output
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

require_sudo() {
    if [[ "$EUID" -ne 0 ]]; then
        if ! sudo -n true 2>/dev/null; then
            log_info "Requesting sudo for reading the Docker volume directory..."
        fi
    fi
}

# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------
cd "${PROJECT_DIR}"

echo ""
echo -e "${C_CYAN}═══════════════════════════════════════════════════════${C_RESET}"
echo -e "${C_CYAN}  dev-down.sh — Stop SQL Server + Backup Volume${C_RESET}"
echo -e "${C_CYAN}═══════════════════════════════════════════════════════${C_RESET}"

# -----------------------------------------------------------------------------
# Step 1 — Stop the container gracefully
# -----------------------------------------------------------------------------
log_info "Step 1/3: Stopping the container..."
if docker compose down; then
    log_success "Container stopped cleanly (SQL Server flushed all writes to disk)"
else
    log_error "docker compose down failed — aborting backup to avoid capturing inconsistent state"
    exit 1
fi
echo ""

# -----------------------------------------------------------------------------
# Step 2 — Snapshot the volume to ~/docker-backups/
# -----------------------------------------------------------------------------
log_info "Step 2/3: Backing up the volume..."

# Verify the volume directory exists on the host before trying to back it up
require_sudo
if ! sudo test -d "${DOCKER_VOLUMES_DIR}/${VOLUME_NAME}"; then
    log_warn "Volume directory not found at ${DOCKER_VOLUMES_DIR}/${VOLUME_NAME}"
    log_warn "Skipping backup. (Was the volume already deleted? Run dev-up.sh to recreate it.)"
    echo ""
    log_success "Shutdown complete (no backup taken)"
    exit 0
fi

# Make sure the backup root exists
mkdir -p "${BACKUP_ROOT}"

# Run rsync as root since the volume is root-owned
# -a archive mode: preserves permissions, timestamps, ownership, symlinks
# --delete:        mirror — remove files in target that no longer exist in source
# --info=progress2: single progress line instead of per-file noise
log_info "Source: ${DOCKER_VOLUMES_DIR}/${VOLUME_NAME}/"
log_info "Target: ${BACKUP_TARGET}/"

if sudo rsync -a --delete --info=progress2 \
       "${DOCKER_VOLUMES_DIR}/${VOLUME_NAME}/" \
       "${BACKUP_TARGET}/"; then
    BACKUP_SIZE="$(sudo du -sh "${BACKUP_TARGET}" 2>/dev/null | cut -f1)"
    log_success "Backup completed (${BACKUP_SIZE})"
else
    log_error "rsync failed — backup is incomplete; older backups are untouched"
    exit 1
fi
echo ""

# -----------------------------------------------------------------------------
# Step 3 — Prune old backups, keeping only the most recent BACKUP_KEEP_COUNT
# -----------------------------------------------------------------------------
log_info "Step 3/3: Pruning old backups (keeping the last ${BACKUP_KEEP_COUNT})..."

# List existing backups, newest first, by name (timestamp prefix sorts correctly)
# Then drop the first BACKUP_KEEP_COUNT — remainder is the prune list
mapfile -t ALL_BACKUPS < <(
    sudo find "${BACKUP_ROOT}" -mindepth 1 -maxdepth 1 -type d \
        -printf '%f\n' 2>/dev/null | sort -r
)

TOTAL_COUNT="${#ALL_BACKUPS[@]}"
if (( TOTAL_COUNT <= BACKUP_KEEP_COUNT )); then
    log_info "Currently have ${TOTAL_COUNT} backup(s) — none to prune."
else
    log_info "Currently have ${TOTAL_COUNT} backup(s) — pruning $((TOTAL_COUNT - BACKUP_KEEP_COUNT))..."
    for (( i = BACKUP_KEEP_COUNT; i < TOTAL_COUNT; i++ )); do
        OLD_DIR="${ALL_BACKUPS[$i]}"
        echo -e "  ${C_DIM}removing  ${BACKUP_ROOT}/${OLD_DIR}${C_RESET}"
        sudo rm -rf "${BACKUP_ROOT}/${OLD_DIR}"
    done
fi
echo ""

# -----------------------------------------------------------------------------
# Final summary
# -----------------------------------------------------------------------------
log_success "All done!"
echo ""
echo -e "${C_DIM}Current backups in ${BACKUP_ROOT}:${C_RESET}"
sudo find "${BACKUP_ROOT}" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' 2>/dev/null \
    | sort -r \
    | while read -r DIR; do
        SIZE="$(sudo du -sh "${BACKUP_ROOT}/${DIR}" 2>/dev/null | cut -f1)"
        echo -e "  ${C_DIM}•${C_RESET} ${DIR}  ${C_DIM}(${SIZE})${C_RESET}"
    done

TOTAL_BACKUP_SIZE="$(sudo du -sh "${BACKUP_ROOT}" 2>/dev/null | cut -f1)"
echo ""
echo -e "${C_DIM}Total backup footprint: ${TOTAL_BACKUP_SIZE}${C_RESET}"
echo ""