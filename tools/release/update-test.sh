#!/usr/bin/env bash
# Shows that a copy players already have will take a new release the next time they open it
# (the user's ask, 2026-11-07: "when we make a release, we should see that you can update the game
# when you open it next"). release.sh runs it twice; it also runs by hand. See docs/releasing.md.
#
#   bash tools/release/update-test.sh                the newest published release updates itself to
#                                                    the build in build/release/ (before publishing)
#   bash tools/release/update-test.sh --old=PATH     the same from another older copy: a folder that
#                                                    holds TheFartherWeStray.exe, or a portable zip
#   bash tools/release/update-test.sh --live=X.Y.Z   after publishing: the address every copy checks
#                                                    says X.Y.Z, and every file it names downloads
#
# The rehearsal, with nothing published: it downloads the newest release's portable zip from GitHub
# (cached in build/update-test/downloads/), unpacks it to build/update-test/run/, and serves
# build/release/ on 127.0.0.1 with a version.json whose addresses point there. It opens the old
# copy (minimized, silent, its user:// in the run folder, so no real save is touched) with
# THEFARTHERWESTRAY_UPDATE_URL at that server. The old copy must find the new version, download it
# and restart; the copy that comes back must say it's up to date, and its .pck and DLL must be
# byte-identical to the new export's. The copies are stopped by their process id when it's done.
# Copies from before 0.3.0 have no updater and can't be rehearsed.
set -euo pipefail
cd "$(dirname "$0")/../.."

REPO=eclancy/TheFartherWeStray
VERSION_URL=https://ecec.dev/the-farther-we-stray/version.json
NAME=TheFartherWeStray
OLD=""
LIVE=""
for arg in "$@"; do
	case "$arg" in
		--old=*) OLD="${arg#--old=}" ;;
		--live=*) LIVE="${arg#--live=}" ;;
		*) echo "usage: bash tools/release/update-test.sh [--old=PATH | --live=X.Y.Z]" >&2; exit 1 ;;
	esac
done

fail() { echo "UPDATE TEST FAILED: $*" >&2; exit 1; }

# A field of a version.json, by its dotted path (builds.windows.patch); empty if it isn't there.
json_field() {
	python -c 'import json, sys
v = json.load(open(sys.argv[1]))
for key in sys.argv[2].split("."):
    v = v.get(key) if isinstance(v, dict) else None
print(v if v is not None else "")' "$1" "$2" | tr -d '\r'
}

# --- After publishing: what every copy will see -----------------------------------------------
if [ -n "$LIVE" ]; then
	mkdir -p build/update-test
	got=build/update-test/live-version.json
	# GitHub's releases/latest can take a little while to move on to a release just made.
	version=""
	for try in $(seq 1 12); do
		if curl -fsSL -m 30 -A "$NAME-release-check" -o "$got" "$VERSION_URL"; then
			version=$(json_field "$got" version)
			[ "$version" = "$LIVE" ] && break
		fi
		sleep 10
	done
	[ "$version" = "$LIVE" ] || fail "$VERSION_URL says ${version:-nothing} after two minutes, not $LIVE: every copy is still being told about the last release"
	for field in builds.windows.patch builds.windows.full installer android; do
		url=$(json_field "$got" "$field")
		[ -n "$url" ] || fail "the live version.json has no $field"
		case "$url" in */releases/download/v$LIVE/*) ;; *) fail "$field points at $url, not v$LIVE's files" ;; esac
		curl -fsIL -m 60 -A "$NAME-release-check" -o /dev/null "$url" || fail "$field ($url) doesn't download"
	done
	echo "Live: $VERSION_URL says $LIVE, and its patch, full zip, installer and APK all download. A copy opened now updates to it."
	exit 0
fi

# --- Before publishing: an older copy updates itself from build/release/ -----------------------
[ -f build/release/version.json ] || fail "no build/release/version.json: run release.sh (--dry-run is enough) first"
[ -f build/windows/$NAME.pck ] || fail "no export in build/windows/"
NEW=$(json_field build/release/version.json version)
[ -n "$NEW" ] || fail "build/release/version.json has no version"

DOWNLOADS=build/update-test/downloads
RUN=build/update-test/run
mkdir -p "$DOWNLOADS"
rm -rf "$RUN"
mkdir -p "$RUN/old" "$RUN/www" "$RUN/appdata"
RUN_W=$(cygpath -w "$(pwd)/$RUN")

# Every copy of the game running from the run folder (only this test starts any there), by id.
copies() {
	RUN_W="$RUN_W" powershell -NoProfile -Command '$w = $env:RUN_W + "\"; Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($w, [StringComparison]::OrdinalIgnoreCase) } | ForEach-Object { $_.ProcessId }' | tr -d '\r'
}
SERVER_PID=""
finish() {
	for pid in $(copies 2>/dev/null); do powershell -NoProfile -Command "Stop-Process -Id $pid -Force" 2>/dev/null || true; done
	[ -n "$SERVER_PID" ] && kill "$SERVER_PID" 2>/dev/null || true
}
trap finish EXIT

# The old copy: the newest published release unless one is named.
if [ -z "$OLD" ]; then
	tag=$(curl -fsSI -m 30 "https://github.com/$REPO/releases/latest" | tr -d '\r' | sed -n 's|^[Ll]ocation: .*/releases/tag/\(v[0-9.]*\)$|\1|p')
	[ -n "$tag" ] || fail "couldn't find the newest release on github.com/$REPO"
	OLD="$DOWNLOADS/$NAME-windows-$tag.zip"
	if [ ! -s "$OLD" ]; then
		echo "Downloading $tag's portable zip, the copy players have now ..."
		curl -fsSL -m 900 -o "$OLD.part" "https://github.com/$REPO/releases/download/$tag/$NAME-windows.zip" || fail "couldn't download $tag's $NAME-windows.zip"
		mv "$OLD.part" "$OLD"
	fi
fi
if [ -d "$OLD" ]; then cp -r "$OLD"/. "$RUN/old/"; else unzip -q "$OLD" -d "$RUN/old" || fail "couldn't unpack $OLD"; fi
OLD_EXE=$(find "$RUN/old" -name "$NAME.exe" | head -1)
[ -n "$OLD_EXE" ] || fail "$OLD has no $NAME.exe"
OLD_DIR=$(dirname "$OLD_EXE")
# Minimized and silent, its output flushed as it prints. Godot reads override.cfg beside the exe;
# no update replaces it, so the copy that restarts keeps it too.
printf '[display]\nwindow/size/mode=1\n[audio]\ndriver/driver="Dummy"\n[application]\nrun/flush_stdout_on_print=true\n' > "$OLD_DIR/override.cfg"

# The server: build/release/'s files on 127.0.0.1, a free port, every request logged.
cp build/release/$NAME-patch.zip build/release/$NAME-windows.zip "$RUN/www/"
cat > "$RUN/serve.py" <<'PY'
import os, sys
try:
    from BaseHTTPServer import HTTPServer
    from SimpleHTTPServer import SimpleHTTPRequestHandler
except ImportError:
    from http.server import HTTPServer, SimpleHTTPRequestHandler
root, port_file, log_file = sys.argv[1:4]
log = open(log_file, "a")
class Handler(SimpleHTTPRequestHandler):
    def log_message(self, fmt, *args):
        log.write((fmt % args) + "\n")
        log.flush()
server = HTTPServer(("127.0.0.1", 0), Handler)
os.chdir(root)
with open(port_file + ".part", "w") as f:
    f.write(str(server.server_address[1]))
os.rename(port_file + ".part", port_file)
server.serve_forever()
PY
python "$RUN/serve.py" "$RUN/www" "$RUN/port" "$RUN/server.log" &
SERVER_PID=$!
for s in $(seq 1 50); do [ -s "$RUN/port" ] && break; sleep 0.2; done
[ -s "$RUN/port" ] || fail "the local server didn't start"
PORT=$(cat "$RUN/port")
LOCAL="http://127.0.0.1:$PORT"
python -c 'import json, sys
v = json.load(open(sys.argv[1]))
local = sys.argv[3] + "/"
def point(url):
    return local + url.rsplit("/", 1)[1]
for kind in v["builds"]["windows"]:
    v["builds"]["windows"][kind] = point(v["builds"]["windows"][kind])
json.dump(v, open(sys.argv[2], "w"), indent=2, sort_keys=True)' build/release/version.json "$RUN/www/version.json" "$LOCAL"

# Open the old copy, as a player would after the release.
LOGS="$RUN/appdata/Godot/app_userdata/The Farther We Stray/logs"
echo "Opening the old copy against $LOCAL/version.json ($NEW) ..."
APPDATA="$(cygpath -w "$(pwd)/$RUN/appdata")" THEFARTHERWESTRAY_UPDATE_URL="$LOCAL/version.json" "$OLD_EXE" > "$RUN/old.log" 2>&1 &
line=""
for s in $(seq 1 120); do
	line=$(grep -h "^UpdateScreen: " "$RUN/old.log" 2>/dev/null | grep -v " is out (" | head -1 || true)
	[ -n "$line" ] && break
	sleep 1
done
[ -n "$line" ] || fail "the old copy said nothing about updating in two minutes (it may predate 0.3.0, which has no updater): see $RUN/old.log"
echo "  old copy: ${line#UpdateScreen: }"
case "$line" in *" -> $NEW ready"*) ;; *) fail "the old copy didn't take $NEW: $line" ;; esac
OLD_VERSION=$(echo "$line" | sed -n 's/^UpdateScreen: \([0-9.]*\) -> .*/\1/p')

# It quits, its script copies the new files over it and opens it again; that copy checks too.
back=""
for s in $(seq 1 120); do
	back=$(grep -h "^UpdateScreen: $NEW is up to date" "$LOGS"/godot.log 2>/dev/null | head -1 || true)
	[ -n "$back" ] && break
	sleep 1
done
[ -n "$back" ] || fail "no copy came back as $NEW within two minutes of the update: see $LOGS/ and $RUN/old.log"
echo "  restarted: ${back#UpdateScreen: }"
finish
trap - EXIT

# What came back is the new version, byte for byte.
for f in "$NAME.pck" "data_${NAME}_windows_x86_64/$NAME.dll"; do
	cmp -s "$OLD_DIR/$f" "build/windows/$f" || fail "the updated copy's $f isn't the new export's"
done
took=$(sed -n 's/.*"GET \/\([^ ]*\) HTTP[^"]*" 200 .*/\1/p' "$RUN/server.log" | grep -v '^version.json$' | head -1)
echo "Update test passed: $OLD_VERSION, opened after $NEW was out, downloaded ${took:-the update}, restarted as $NEW and said it was up to date; its .pck and DLL are the new export's, byte for byte."
