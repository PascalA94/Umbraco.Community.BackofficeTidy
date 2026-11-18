#!/usr/bin/env bash
# Release gate. Two tag shapes:
#  - release/<PackageMinor>.<PackagePatch>-<suffix> (suffix optional) packs the 17 and 18 lines, for example
#    release/0.0-alpha.1 gives 17.0.0-alpha.1 and 18.0.0-alpha.1. Fails if Directory.Build.props disagrees with the tag.
#  - release/19.<minor>.<patch>-<suffix> (suffix optional) packs line 19 only, for example release/19.0.0-rc.1. The
#    version is the tag text; pack.sh passes it as -p:Version, so there is nothing in Directory.Build.props to match.
# Either way a client version file that is not the 0.0.0 placeholder fails (the pack step stamps the real version).
# Usage: build/check-release-version.sh release/0.0-alpha.1. Run from the repository root.
set -euo pipefail

tag="${1:?usage: check-release-version.sh <release/minor.patch[-suffix] | release/19.minor.patch[-suffix]>}"
want="${tag#release/}"
pkg="src/Umbraco.Community.BackofficeTidy"
status=0
sfx='(-[0-9A-Za-z][0-9A-Za-z.-]*)?'

if [[ "$tag" =~ ^release/19\.[0-9]+\.[0-9]+${sfx}$ ]]; then
  : # 19-only tag: the version comes from the tag (pack.sh passes -p:Version), nothing in Directory.Build.props to match
elif [[ "$tag" =~ ^release/[0-9]+\.[0-9]+${sfx}$ ]]; then
  prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" Directory.Build.props | head -1; }
  minor="$(prop PackageMinor)"; patch="$(prop PackagePatch)"; suffix="$(prop PackageVersionSuffix)"
  have="$minor.$patch${suffix:+-$suffix}"
  if [ "$have" != "$want" ]; then
    echo "check-release-version: Directory.Build.props has '$have', tag wants '$want'" >&2
    status=1
  fi
else
  echo "check-release-version: unexpected tag '$tag'" >&2
  exit 1
fi

for f in "$pkg/Client/package.json" "$pkg/Client/public/umbraco-package.json" "$pkg/wwwroot/umbraco-package.json" "$pkg/Client/package-lock.json"; do
  got="$(node -p "require('./$f').version")"
  if [ "$got" != "0.0.0" ]; then
    echo "check-release-version: $f has '$got', expected the 0.0.0 placeholder" >&2
    status=1
  fi
done

exit $status
