#!/usr/bin/env bash
# Build The Farther We Stray for Windows: build/windows/TheFartherWeStray-windows.zip, a folder to
# unzip anywhere and run (TheFartherWeStray.exe, its .pck and the .NET assemblies beside it).
#
#   tools/windows-build.sh          export and zip
#   tools/windows-build.sh --run    then start the exported game, to try it
#
# Needs $GODOT_BIN (the console exe; see .claude/settings.json) and Godot's 4.5.1 mono export
# templates. Uses the "Windows Desktop" preset in export_presets.cfg. Not code-signed, so Windows
# SmartScreen warns on first run ("More info", then "Run anyway").
set -u
cd "$(dirname "$0")/.." || exit 1

DIR="build/windows/TheFartherWeStray"
EXE="$DIR/TheFartherWeStray.exe"
ZIP="build/windows/TheFartherWeStray-windows.zip"

if [ -z "${GODOT_BIN:-}" ] || [ ! -e "$GODOT_BIN" ]; then echo "Missing: GODOT_BIN" >&2; exit 1; fi

rm -rf "$DIR" "$ZIP"
mkdir -p "$DIR"
echo "Exporting ..."
timeout 1800 "$GODOT_BIN" --headless --path . --export-release "Windows Desktop" "$EXE" > build/windows/export.log 2>&1
if [ ! -s "$EXE" ] || [ ! -s "${EXE%.exe}.pck" ]; then
	echo "No game was written. The end of build/windows/export.log:" >&2
	tail -30 build/windows/export.log >&2
	exit 1
fi

echo "Zipping ..."
powershell -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$DIR")' -DestinationPath '$(cygpath -w "$ZIP")' -Force" || exit 1
echo "Built $ZIP ($(( $(stat -c %s "$ZIP") / 1048576 )) MB)"

if [ "${1:-}" = "--run" ]; then "$EXE" & fi
