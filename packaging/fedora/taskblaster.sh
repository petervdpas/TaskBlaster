#!/usr/bin/bash
set -euo pipefail

APPDIR="/opt/TaskBlaster"
export LD_LIBRARY_PATH="$APPDIR:${LD_LIBRARY_PATH:-}"

exec "$APPDIR/TaskBlaster" "$@"
