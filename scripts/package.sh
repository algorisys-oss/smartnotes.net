#!/usr/bin/env bash
# Build a release of YappyNotes for one runtime identifier.
#
#   scripts/package.sh linux-x64
#   scripts/package.sh osx-arm64 --publish-only
#
# Runtime identifiers: linux-x64 linux-arm64 win-x64 win-arm64 osx-x64 osx-arm64
#
# Prints THE ARTIFACT PATH ON STDOUT AND NOTHING ELSE; build logs go to stderr,
# because callers capture the path with $(...).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"
publish_only=0
[[ "${2:-}" == "--publish-only" ]] && publish_only=1

case "$rid" in
  linux-x64|linux-arm64|win-x64|win-arm64|osx-x64|osx-arm64) ;;
  *) echo "usage: $(basename "$0") <rid> [--publish-only]" >&2; exit 2 ;;
esac

version="$("$root/scripts/version.sh")"
staging="$root/artifacts/publish/$rid"
rm -rf "$staging"

# Self-contained so there is no runtime to install first. Not single-file:
# Avalonia's native libraries want to be real files on disk, and a note-taking
# app is not worth the debugging that hiding them invites.
dotnet publish "$root/src/YappyNotes.App/YappyNotes.App.csproj" \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  -p:PublishSingleFile=false \
  --output "$staging" >&2

if [[ $publish_only -eq 1 ]]; then
  echo "$staging"
  exit 0
fi

mkdir -p "$root/artifacts"
archive="$root/artifacts/yappynotes-$version-$rid"

if [[ "$rid" == win-* ]]; then
  archive="$archive.zip"
  rm -f "$archive"
  (cd "$staging" && zip -qr "$archive" .)
else
  archive="$archive.tar.gz"
  rm -f "$archive"
  tar -czf "$archive" -C "$staging" .
fi

echo "$archive"
