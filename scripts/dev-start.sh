#!/usr/bin/env bash
# Run SmartNotes for development: a Debug build, so the F12 developer tools are
# compiled in.
#
#   --watch     rebuild and restart on any source change
#   --sandbox   use a throwaway database under artifacts/, leaving your real
#               notes alone. Use it whenever you are about to touch the schema.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/src/SmartNotes.App/SmartNotes.App.csproj"

watch=0
sandbox=0
for arg in "$@"; do
  case "$arg" in
    --watch)   watch=1 ;;
    --sandbox) sandbox=1 ;;
    -h|--help) sed -n '2,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

if [[ $sandbox -eq 1 ]]; then
  # UserPaths honours this, so a sandbox run never opens the real notes.db.
  export SMARTNOTES_DATA_DIR="$root/artifacts/sandbox"
  mkdir -p "$SMARTNOTES_DATA_DIR"
  echo "sandbox: $SMARTNOTES_DATA_DIR" >&2
fi

if [[ $watch -eq 1 ]]; then
  exec dotnet watch --project "$project" run --configuration Debug
fi

exec dotnet run --project "$project" --configuration Debug
