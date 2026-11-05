#!/usr/bin/env bash
# Release gate. One tag packs both Umbraco lines: release/<PackageMinor>.<PackagePatch>-<suffix> (suffix optional),
# for example release/0.0-alpha.1 gives 17.0.0-alpha.1 and 18.0.0-alpha.1. Fails if Directory.Build.props disagrees with
# the tag, or a client version file is not the 0.0.0 placeholder (the pack step stamps the real version).
# Usage: build/check-release-version.sh release/0.0-alpha.1. Run from the repository root.
set -euo pipefail

tag="${1:?usage: check-release-version.sh <release/minor.patch[-suffix]>}"
want="${tag#release/}"
pkg="src/Umbraco.Community.BackofficeTidy"
status=0

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" Directory.Build.props | head -1; }
minor="$(prop PackageMinor)"; patch="$(prop PackagePatch)"; suffix="$(prop PackageVersionSuffix)"
have="$minor.$patch${suffix:+-$suffix}"
if [ "$have" != "$want" ]; then
  echo "check-release-version: Directory.Build.props has '$have', tag wants '$want'" >&2
  status=1
fi

for f in "$pkg/Client/package.json" "$pkg/Client/public/umbraco-package.json" "$pkg/wwwroot/umbraco-package.json" "$pkg/Client/package-lock.json"; do
  got="$(node -p "require('./$f').version")"
  if [ "$got" != "0.0.0" ]; then
    echo "check-release-version: $f has '$got', expected the 0.0.0 placeholder" >&2
    status=1
  fi
done

exit $status
