#!/usr/bin/env bash
# The one place a version number is read from. Directory.Build.props is the one
# place it is written: VersionPrefix, plus VersionSuffix for a prerelease.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
props="$root/Directory.Build.props"

prefix="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$props" | head -1)"
suffix="$(sed -n 's:.*<VersionSuffix>\(.*\)</VersionSuffix>.*:\1:p' "$props" | head -1)"

if [[ -n "$suffix" ]]; then
  echo "$prefix-$suffix"
else
  echo "$prefix"
fi
