#!/bin/sh
# Dump public signatures from a Dalamud-shipped assembly.
#   api.sh <Assembly.dll> <type-name regex> [<regex> ...]
# Examples:
#   api.sh Dalamud.dll 'Services\.INamePlateGui$'
#   api.sh FFXIVClientStructs.dll 'AddonNamePlate\+NamePlateObject$'
#   api.sh Dalamud.Bindings.ImGui.dll '^Dalamud\.Bindings\.ImGui\.ImGui$' | grep ' Combo('
# Nested types use '+', e.g. 'GroupManager\+Group$'. The first run builds the tool (a few seconds).
set -e
here=$(cd "$(dirname "$0")" && pwd)
dalamud=${DALAMUD_HOME:-$HOME/.xlcore/dalamud/Hooks/dev}
out=${XDG_CACHE_HOME:-$HOME/.cache}/dalamud-apidump
if [ ! -f "$out/apidump.dll" ] || [ "$here/apidump/Program.cs" -nt "$out/apidump.dll" ]; then
  dotnet build "$here/apidump/apidump.csproj" -c Release -o "$out" \
    -p:BaseIntermediateOutputPath="$out/obj/" >/dev/null
fi
exec dotnet "$out/apidump.dll" "$dalamud" "$@"
