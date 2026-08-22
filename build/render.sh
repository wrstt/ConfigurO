#!/usr/bin/env bash
# Renders the Nocturne UI to PNGs without a Windows desktop. Dev tool only.
set -uo pipefail
cd "$(dirname "$0")/.."
export PATH="/home/linuxbrew/.linuxbrew/bin:$PATH"
export LD_LIBRARY_PATH="/home/linuxbrew/.linuxbrew/lib:${LD_LIBRARY_PATH:-}"

MONO_LIB=/home/linuxbrew/.linuxbrew/Cellar/mono/6.14.1/lib/mono/4.8-api
OUT=build/render/out
mkdir -p "$OUT"

REFS=(System System.Core System.Drawing System.Windows.Forms System.Data System.Xml
      System.Xml.Linq System.Management System.ServiceProcess System.Net.Http
      System.Data.DataSetExtensions Microsoft.CSharp Microsoft.VisualBasic
      System.IO.Compression System.Deployment System.Configuration)
ARGS=(-target:exe -out:build/render/render.exe -langversion:latest -nostdlib+
      -define:MONO_LINUX_CHECK -main:ConfigurO.RenderHarness
      -nowarn:0169,0414,0649,0067,1591,0162,0219,0618,1702)
for r in "${REFS[@]}"; do ARGS+=("-r:$MONO_LIB/$r.dll"); done
ARGS+=("-r:$MONO_LIB/mscorlib.dll" "-r:src/ConfigurO/Newtonsoft.Json.dll")

# The .resx has to become a .resources the harness assembly can embed, or
# every Properties.Resources lookup throws MissingManifestResourceException.
RES=build/render/ConfigurO.Properties.Resources.resources
if [ ! -f "$RES" ]; then
  echo "missing $RES -- run build/render-resources.sh first" >&2
  exit 1
fi
ARGS+=("-resource:$RES,ConfigurO.Properties.Resources.resources")

mapfile -t SRC < <(find src/ConfigurO -name '*.cs' | sort)
mcs "${ARGS[@]}" "${SRC[@]}" build/psstub.cs build/render/RenderHarness.cs 2>&1 | grep 'error' && exit 1
# Json.NET has to sit beside the harness for the runtime to resolve it.
cp -f src/ConfigurO/Newtonsoft.Json.dll build/render/
mono build/render/render.exe "$OUT"
