#!/usr/bin/env bash

set -euo pipefail

_db_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
_db_repo_dir="$(cd "${_db_script_dir}/.." && pwd)"

migrate_db() {
    local conn="${1:-${DB_CONNECTION_STRING:-${DATABASE_URL:-}}}"

    if [[ -z "${conn}" ]]; then
        echo "Usage: migrate_db <connection-string>" >&2
        echo "Or set DB_CONNECTION_STRING / DATABASE_URL." >&2
        return 1
    fi

    if ! command -v psql >/dev/null 2>&1; then
        echo "psql not found in PATH." >&2
        return 1
    fi

    local sql_dir="${_db_repo_dir}/sql"

    echo "Applying schema..."
    psql "${conn}" -v ON_ERROR_STOP=1 -f "${sql_dir}/01_schema.sql"

    echo "Applying triggers..."
    psql "${conn}" -v ON_ERROR_STOP=1 -f "${sql_dir}/02_triggers.sql"

    echo "Applying functions..."
    psql "${conn}" -v ON_ERROR_STOP=1 -f "${sql_dir}/03_functions.sql"

    if [[ "${APPLY_PGAGENT_JOB:-0}" == "1" ]]; then
        echo "Applying pgAgent job..."
        psql "${conn}" -v ON_ERROR_STOP=1 -f "${sql_dir}/04_pgagent_job.sql"
    fi

    echo "Database migration finished."
}

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    migrate_db "${@:-}"
fi
