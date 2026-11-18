#!/usr/bin/env bash
# Packs one nupkg per Umbraco line into <outdir> (default ./artifacts), each with the client bundle.
# LINES (space separated) picks the lines; the default is every line in eng/lines.txt.
# PACK_VERSION (exact version, for a 19-only tag such as 19.0.0-rc.1) overrides the version built from Directory.Build.props.
# The client version files stay 0.0.0 in git; this stamps the line's package version into wwwroot/umbraco-package.json
# for the build and pack, then restores it, so the committed tree never changes.
# Usage: build/pack.sh [outdir]. Run from the repository root, after the client build and the tests.
set -euo pipefail
cd "$(dirname "$0")/.."
out="${1:-./artifacts}"
proj=src/Umbraco.Community.BackofficeTidy/Umbraco.Community.BackofficeTidy.csproj
manifest=src/Umbraco.Community.BackofficeTidy/wwwroot/umbraco-package.json
backup="$(mktemp)"
cp "$manifest" "$backup"
trap 'cp "$backup" "$manifest"; rm -f "$backup"' EXIT
mkdir -p "$out"

lines="${LINES:-$(tr '\n' ' ' < eng/lines.txt)}"
vprop=()
[ -z "${PACK_VERSION:-}" ] || vprop=("-p:Version=$PACK_VERSION")

# Line 19 has no PackageMinor/PackagePatch version of its own: without PACK_VERSION it would pack as a stable-looking 19.0.1.
case " $lines " in *" 19 "*) [ -n "${PACK_VERSION:-}" ] || { echo "line 19 needs PACK_VERSION (for example 19.0.0-rc.1)" >&2; exit 1; };; esac
[ -z "${PACK_VERSION:-}" ] || [ "$(wc -w <<<"$lines" | tr -d ' ')" -eq 1 ] || { echo "PACK_VERSION needs exactly one line in LINES" >&2; exit 1; }

for line in $lines; do
  [ -z "${PACK_VERSION:-}" ] || [ "${PACK_VERSION%%.*}" = "$line" ] || { echo "PACK_VERSION $PACK_VERSION does not match line $line" >&2; exit 1; }
  version="$(dotnet msbuild "$proj" "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -getProperty:Version | tr -d '[:space:]')"
  case "$version" in ''|*-) echo "invalid package version '$version' for line $line" >&2; exit 1;; esac
  node -e '
    const fs = require("fs"); const [f, v] = process.argv.slice(1);
    const j = JSON.parse(fs.readFileSync(f, "utf8")); j.version = v;
    fs.writeFileSync(f, JSON.stringify(j, null, 2) + "\n");' "$manifest" "$version"
  dotnet restore "$proj" "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} --locked-mode -v q
  dotnet build "$proj" -c Release --no-restore "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -v q
  dotnet pack "$proj" -c Release --no-build "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -o "$out" -v q
  cp "$backup" "$manifest"
  echo "packed $line: $version"
done
