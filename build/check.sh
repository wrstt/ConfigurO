#!/usr/bin/env bash
# Linux type-check harness for ConfigurO.
#
# The shipping build is MSBuild on Windows (src/ConfigurO/ConfigurO.csproj,
# .NET Framework 4.8). This script exists only so the C# can be compiled and
# type-checked on a Linux dev box using Mono's reference assemblies. It stubs
# System.Management.Automation (Windows-only) and skips resource embedding.
#
# Usage: build/check.sh [--quiet]
set -uo pipefail
cd "$(dirname "$0")/.."

MONO_LIB=/home/linuxbrew/.linuxbrew/Cellar/mono/6.14.1/lib/mono/4.8-api
OUT=build/out
mkdir -p "$OUT"

REFS=(
  System System.Core System.Drawing System.Windows.Forms System.Data
  System.Xml System.Xml.Linq System.Management System.ServiceProcess
  System.Net.Http System.Data.DataSetExtensions Microsoft.CSharp
  Microsoft.VisualBasic System.IO.Compression System.IO.Compression.FileSystem
  System.Deployment System.Configuration
)
ARGS=(-target:library -out:"$OUT/ConfigurO.dll" -langversion:latest -nostdlib+
      -define:MONO_LINUX_CHECK -nowarn:0169,0414,0649,0067,1591,0162,0219,0618)
for r in "${REFS[@]}"; do ARGS+=("-r:$MONO_LIB/$r.dll"); done
ARGS+=("-r:$MONO_LIB/mscorlib.dll")
ARGS+=("-r:src/ConfigurO/Newtonsoft.Json.dll")

mapfile -t SRC < <(find src/ConfigurO -name '*.cs' | sort | grep -v -F -f <(grep -v '^#' build/exclude.txt | grep -v '^$'))
SRC+=(build/psstub.cs)

mcs "${ARGS[@]}" "${SRC[@]}" 2>&1 | grep -v '^Compilation\|^$' > "$OUT/errors.txt"
ERRS=$(grep -c 'error CS' "$OUT/errors.txt" || true)
WARNS=$(grep -c 'warning CS' "$OUT/errors.txt" || true)
echo "errors: $ERRS   warnings: $WARNS"
if [ "${1:-}" != "--quiet" ]; then
  grep 'error CS' "$OUT/errors.txt" | head -60
fi
[ "$ERRS" -eq 0 ]
