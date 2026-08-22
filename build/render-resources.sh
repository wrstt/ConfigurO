#!/usr/bin/env bash
# Compiles src/ConfigurO/Properties/Resources.resx into a .resources file the
# headless render harness can embed. Dev tool only -- on Windows MSBuild does
# this as part of the normal build.
#
# Mono's resgen lowercases ResXFileRef paths, which a case-sensitive
# filesystem then cannot find, so the referenced tree is staged with
# lowercase symlinks alongside the real names.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="/home/linuxbrew/.linuxbrew/bin:$PATH"
export LD_LIBRARY_PATH="/home/linuxbrew/.linuxbrew/lib:${LD_LIBRARY_PATH:-}"

STAGE=build/render/stage
rm -rf "$STAGE"
mkdir -p "$STAGE/Properties"
cp -r src/ConfigurO/Resources "$STAGE/Resources"
ln -sfn Resources "$STAGE/resources"

python3 - "$STAGE" <<'PY'
import os, re, sys
stage = sys.argv[1]
for dp, dns, fns in os.walk(os.path.join(stage, 'Resources')):
    for name in list(dns) + fns:
        low = name.lower()
        if low != name and not os.path.exists(os.path.join(dp, low)):
            os.symlink(name, os.path.join(dp, low))
s = open('src/ConfigurO/Properties/Resources.resx', encoding='utf-8').read()
s = re.sub(r'<value>([^;<]+);', lambda m: '<value>' + m.group(1).replace('\\', '/') + ';', s)
open(os.path.join(stage, 'Properties', 'Resources.resx'), 'w', encoding='utf-8').write(s)
PY

( cd "$STAGE/Properties" && resgen2 Resources.resx ../../ConfigurO.Properties.Resources.resources )
echo "wrote build/render/ConfigurO.Properties.Resources.resources"
