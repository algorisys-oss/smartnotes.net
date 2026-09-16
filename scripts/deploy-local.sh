#!/usr/bin/env bash
# Build a release for this machine and install it into a local tools folder.
#
#   scripts/deploy-local.sh                  # into ~/Desktop/tools/yappynotes
#   scripts/deploy-local.sh /opt/tools       # into /opt/tools/yappynotes
#
# The whole publish folder is copied, not just the executable: releases are not
# single-file, and YappyNotes.App will not start without the native libraries
# beside it.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tools="${1:-$HOME/Desktop/tools}"
target="$tools/yappynotes"

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64)            rid=linux-x64 ;;
  Linux-aarch64)           rid=linux-arm64 ;;
  Darwin-x86_64)           rid=osx-x64 ;;
  Darwin-arm64)            rid=osx-arm64 ;;
  *) echo "unsupported platform: $(uname -s) $(uname -m)" >&2; exit 2 ;;
esac

staging="$("$root/scripts/package.sh" "$rid" --publish-only)"

# Copy beside the old install and swap, rather than overwriting in place: a
# running YappyNotes has its libraries mapped, and rewriting them under it
# crashes it instead of letting it carry on until it is restarted.
mkdir -p "$tools"
rm -rf "$target.new" "$target.old"
cp -a "$staging" "$target.new"
[[ -d "$target" ]] && mv "$target" "$target.old"
mv "$target.new" "$target"
rm -rf "$target.old"

echo "$target/YappyNotes.App"
