#!/usr/bin/env bash
# The one place a version number is read from. Directory.Build.props is the one
# place it is written.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$root/Directory.Build.props" | head -1
