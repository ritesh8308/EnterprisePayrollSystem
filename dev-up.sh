#!/bin/bash
cd "$(dirname "$0")"
docker compose up -d
echo "Waiting for SQL Server to be ready..."
sleep 3
docker exec payroll-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Pass123' -C -Q "SELECT 1" > /dev/null 2>&1 && \
  echo "✅ SQL Server is ready" || echo "⚠️  SQL Server still starting"
