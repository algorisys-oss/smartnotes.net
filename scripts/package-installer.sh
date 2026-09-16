#!/usr/bin/env bash
# Build the self-updating installer for one runtime identifier, with Velopack.
#
#   scripts/package-installer.sh linux-x64
#
# Prints THE OUTPUT FOLDER ON STDOUT AND NOTHING ELSE, like package.sh, because
# callers capture it with $(...). Build logs go to stderr.
#
# The folder holds what Velopack makes for that runtime: the installer (Setup.exe,
# a .pkg, an .AppImage), a portable build, the .nupkg an installed copy downloads,
# and releases.<rid>.json, which is how an installed copy finds that .nupkg on a
# GitHub release. The channel is the runtime identifier, so a linux-arm64 install
# is only ever offered a linux-arm64 release.
#
# vpk packs only for the OS it runs on, so each runtime has to be packed on its own
# platform: win-* on Windows, osx-* on macOS, linux-* on Linux.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"

case "$rid" in
  linux-x64|linux-arm64) os=Linux;   exe=YappyNotes.App;     icon=yappynotes.png ;;
  win-x64|win-arm64)     os=Windows; exe=YappyNotes.App.exe; icon=yappynotes.ico ;;
  osx-x64|osx-arm64)     os=Darwin;  exe=YappyNotes.App;     icon=yappynotes.icns ;;
  *) echo "usage: $(basename "$0") <rid>" >&2; exit 2 ;;
esac

here="$(uname -s)"
case "$here" in MINGW*|MSYS*|CYGWIN*) here=Windows ;; esac
if [[ "$here" != "$os" ]]; then
  echo "$rid has to be packed on $os; this is $here." >&2
  exit 2
fi

version="$("$root/scripts/version.sh")"
staging="$("$root/scripts/package.sh" "$rid" --publish-only)"
output="$root/artifacts/installers/$rid"
rm -rf "$output"

(cd "$root" && dotnet tool restore >&2)

(cd "$root" && dotnet vpk pack \
  --packId YappyNotes \
  --packTitle YappyNotes \
  --packAuthors "YappyNotes contributors" \
  --packVersion "$version" \
  --packDir "$staging" \
  --mainExe "$exe" \
  --icon "$root/src/YappyNotes.App/Assets/$icon" \
  --runtime "$rid" \
  --channel "$rid" \
  --outputDir "$output" >&2)

echo "$output"
