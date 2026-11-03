#!/usr/bin/env bash
# Publish a new version of The Farther We Stray (after The One He Missed's release.sh). Every
# Windows copy installs it the next time it starts; every Android copy says on its title that it's
# out and offers the APK.
#
#   bash tools/release/release.sh 0.3.0 ["What changed, for players"]
#   bash tools/release/release.sh --dry-run 0.3.0     everything but the commit, tag, push and release
#
# Sets the version (project.godot) and the Android version code (export_presets.cfg), exports
# Windows and packages it (tools/release/package.py: the portable zip, the small patch zip,
# version.json), compiles the installer (installer.iss, Inno Setup), exports and checks the signed
# APK, commits "Release vX.Y.Z", tags, pushes, and makes the GitHub Release with all of them.
# ecec.dev/the-farther-we-stray/version.json redirects to the newest release's version.json, so
# nothing on ecec.dev needs deploying for a release. See docs/releasing.md.
#
# --dry-run builds every file into build/release/ and checks the APK's signature, then puts
# project.godot and export_presets.cfg back: no commit, tag, push or release.
set -euo pipefail

DRY_RUN=false
ARGS=()
for arg in "$@"; do
	if [ "$arg" = "--dry-run" ]; then DRY_RUN=true; else ARGS+=("$arg"); fi
done
VERSION="${ARGS[0]:-}"
CHANGES="${ARGS[1]:-}"
HOW_TO="Download **TheFartherWeStray-Setup.exe** and run it, then play from the Start menu or the desktop: it keeps itself up to date from then on. No installer? TheFartherWeStray-windows.zip is the portable version: unzip it anywhere you can write to and run TheFartherWeStray.exe (it updates itself too). On an Android phone, download TheFartherWeStray-android.apk on the phone and open it (allow your browser to install apps when Android asks); the game says on its title screen when a newer version is out. Download page: https://ecec.dev/the-farther-we-stray/download"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "usage: bash tools/release/release.sh [--dry-run] X.Y.Z [\"what changed\"]" >&2
	exit 1
fi

cd "$(dirname "$0")/../.."

# --- The Android version code --------------------------------------------------------------
# Android installs an APK over an older one only if its versionCode is higher, so it must climb
# with every release: major*10000 + minor*100 + patch (0.3.1 is 301), monotonic while minor and
# patch stay under 100.
IFS=. read -r V_MAJOR V_MINOR V_PATCH <<<"$VERSION"
V_MAJOR=$((10#$V_MAJOR)); V_MINOR=$((10#$V_MINOR)); V_PATCH=$((10#$V_PATCH))
if [ "$V_MINOR" -ge 100 ] || [ "$V_PATCH" -ge 100 ]; then
	echo "minor and patch must stay under 100, or the Android version code stops climbing" >&2
	exit 1
fi
VERSION_CODE=$((V_MAJOR * 10000 + V_MINOR * 100 + V_PATCH))
CURRENT_CODE=$(sed -n 's/^version\/code=\([0-9]*\)$/\1/p' export_presets.cfg)
[ -n "$CURRENT_CODE" ] || { echo "export_presets.cfg has no version/code line" >&2; exit 1; }
if [ "$VERSION_CODE" -le "$CURRENT_CODE" ]; then
	echo "Android version code $VERSION_CODE (from $VERSION) isn't above the last one, $CURRENT_CODE: Android would refuse it as an update" >&2
	exit 1
fi

# A Claude session has GODOT_BIN from .claude/settings.json; a plain shell reads it from there.
if [ -z "${GODOT_BIN:-}" ] && [ -f .claude/settings.json ]; then
	GODOT_BIN=$(sed -n 's/^ *"GODOT_BIN": *"\(.*\)",\{0,1\} *$/\1/p' .claude/settings.json | sed 's/\\\\/\\/g')
	GODOT_BIN=$(cygpath -u "$GODOT_BIN" 2>/dev/null || echo "$GODOT_BIN")
	export GODOT_BIN
fi
: "${GODOT_BIN:?GODOT_BIN is not set - see .claude/settings.json}"
[ -f "$GODOT_BIN" ] || { echo "GODOT_BIN doesn't exist: $GODOT_BIN" >&2; exit 1; }

# Inno Setup compiles the installer (winget install JRSoftware.InnoSetup puts it here, per user).
ISCC="${ISCC:-$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe}"
ISCC=$(cygpath -u "$ISCC" 2>/dev/null || echo "$ISCC")
[ -f "$ISCC" ] || { echo "Inno Setup's ISCC.exe isn't at $ISCC: install it (winget install JRSoftware.InnoSetup) or set ISCC" >&2; exit 1; }

# --- Android, all checked before anything is touched ----------------------------------------
# The release keystore and its password live outside the repo, which is public: the password is
# read into the environment of the Godot export and of keytool alone, never a file in the repo, a
# log or a command line. Lose either file and no Android copy can ever be updated again.
GODOT_DATA="$(cygpath -u "${APPDATA:-}" 2>/dev/null || echo "${APPDATA:-}")/Godot"
KEYSTORE="${THEFARTHERWESTRAY_KEYSTORE:-$GODOT_DATA/keystores/thefartherwestray-release.keystore}"
KEYSTORE_SECRET="${KEYSTORE%.keystore}.secret"
[ -f "$KEYSTORE" ] || { echo "release keystore not found: $KEYSTORE" >&2; exit 1; }
[ -f "$KEYSTORE_SECRET" ] || { echo "keystore secret not found: $KEYSTORE_SECRET" >&2; exit 1; }
KEY_ALIAS=$(sed -n 's/^alias=//p' "$KEYSTORE_SECRET" | tr -d '\r')
[ -n "$KEY_ALIAS" ] || { echo "$KEYSTORE_SECRET has no alias= line" >&2; exit 1; }
grep -q '^password=.' "$KEYSTORE_SECRET" || { echo "$KEYSTORE_SECRET has no password= line" >&2; exit 1; }

EDITOR_SETTINGS=$(ls "$GODOT_DATA"/editor_settings-4.*.tres 2>/dev/null | sort -V | tail -1 || true)
editor_setting() {
	[ -n "$EDITOR_SETTINGS" ] && sed -n "s|^export/android/$1 = \"\(.*\)\"\$|\1|p" "$EDITOR_SETTINGS"
	return 0
}
ANDROID_SDK="${ANDROID_SDK_ROOT:-$(editor_setting android_sdk_path)}"
ANDROID_SDK=$(cygpath -u "$ANDROID_SDK" 2>/dev/null || echo "$ANDROID_SDK")
BUILD_TOOLS=$(ls -d "$ANDROID_SDK"/build-tools/* 2>/dev/null | sort -V | tail -1 || true)
APKSIGNER="$BUILD_TOOLS/apksigner.bat"
[ -f "$APKSIGNER" ] || APKSIGNER="$BUILD_TOOLS/apksigner"
AAPT="$BUILD_TOOLS/aapt.exe"
[ -f "$AAPT" ] || AAPT="$BUILD_TOOLS/aapt"
[ -f "$APKSIGNER" ] && [ -f "$AAPT" ] || { echo "no apksigner/aapt under $ANDROID_SDK/build-tools: set ANDROID_SDK_ROOT" >&2; exit 1; }
JDK="${JAVA_HOME:-$(editor_setting java_sdk_path)}"
JDK=$(cygpath -u "$JDK" 2>/dev/null || echo "$JDK")
KEYTOOL="$JDK/bin/keytool.exe"
[ -f "$KEYTOOL" ] || KEYTOOL="$JDK/bin/keytool"
[ -f "$KEYTOOL" ] || { echo "keytool not found under $JDK: set JAVA_HOME" >&2; exit 1; }
export JAVA_HOME="$JDK" # for apksigner

# A release is built from what's committed. Docs are left out of the check: they're never in a
# build (the presets exclude docs/), and a story session may be writing in them meanwhile.
if [ -n "$(git status --porcelain -- . ':!docs')" ]; then
	echo "commit or stash your changes first: a release is built from what's committed" >&2
	git status --porcelain -- . ':!docs' >&2
	exit 1
fi
if ! $DRY_RUN && git rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null; then
	echo "v$VERSION already exists" >&2
	exit 1
fi

CURRENT=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project.godot)
if $DRY_RUN; then echo "The Farther We Stray $CURRENT -> $VERSION (dry run: nothing is committed or published)"; else echo "The Farther We Stray $CURRENT -> $VERSION"; fi

# Until the release commit exists, any way out (a failed export, Ctrl+C, the end of a dry run) puts
# the two version files back, so a failure never leaves a half-bumped tree.
COMMITTED=false
restore_versions() { $COMMITTED || git checkout -q -- project.godot export_presets.cfg; }
trap restore_versions EXIT

sed -i "s/^config\/version=\".*\"$/config\/version=\"$VERSION\"/" project.godot
sed -i "s/^version\/code=[0-9]*$/version\/code=$VERSION_CODE/" export_presets.cfg

dotnet build TheFartherWeStray.sln -nologo -v q
rm -rf build/windows build/release build/android
mkdir -p build/windows build/release build/android

# Godot can finish an export, print its savepack DONE line and then never exit, so a timeout after
# that line is a finished export, judged by its log and its .pck.
rc=0
timeout 900 "$GODOT_BIN" --headless --path . --export-release "Windows Desktop" build/windows/TheFartherWeStray.exe > build/export.log 2>&1 || rc=$?
if [ "$rc" -ne 0 ] && ! { [ "$rc" -eq 124 ] && grep -q "DONE.*savepack" build/export.log; }; then
	tail -20 build/export.log; echo "the Windows export failed: see build/export.log" >&2; exit 1
fi
[ -f build/windows/TheFartherWeStray.pck ] || { echo "the export wrote no .pck: see build/export.log" >&2; exit 1; }

python tools/release/package.py "$VERSION"
# The MSYS variables stop Git Bash rewriting ISCC's /Q and /D switches into Windows paths.
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' "$ISCC" /Q "/DAppVersion=$VERSION" tools/release/installer.iss \
	|| { echo "the installer didn't compile" >&2; exit 1; }
[ -f build/release/TheFartherWeStray-Setup.exe ] || { echo "ISCC wrote no TheFartherWeStray-Setup.exe" >&2; exit 1; }

# --- The Android APK ------------------------------------------------------------------------
with_signing() {
	(
		export GODOT_ANDROID_KEYSTORE_RELEASE_PATH="$(cygpath -w "$KEYSTORE" 2>/dev/null || echo "$KEYSTORE")"
		export GODOT_ANDROID_KEYSTORE_RELEASE_USER="$KEY_ALIAS"
		GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD="$(sed -n 's/^password=//p' "$KEYSTORE_SECRET" | tr -d '\r')"
		export GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD
		"$@"
	)
}
password_in() { with_signing sh -c 'grep -qF -- "$GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD" "$1"' _ "$1"; }

APK=build/android/TheFartherWeStray.apk
echo "Exporting the Android APK ..."
# The headless export can write the APK and then not exit, so it's judged by the APK it wrote.
with_signing timeout 1800 "$GODOT_BIN" --headless --path . --export-release "Android" "$APK" > build/android/export.log 2>&1 || true
if password_in build/android/export.log; then
	rm -f build/android/export.log
	echo "the export log held the keystore password: deleted it; don't publish this build" >&2
	exit 1
fi
[ -s "$APK" ] || { echo "the Android export wrote no APK: see build/android/export.log" >&2; exit 1; }

# Signed with OUR key: one signed with a debug key would install, and then no later release could
# ever be installed over it.
SIGNERS=$("$APKSIGNER" verify --print-certs "$APK" 2>/dev/null) || { echo "apksigner says $APK doesn't verify" >&2; exit 1; }
APK_CERT=$(echo "$SIGNERS" | sed -n 's/^Signer #1 certificate SHA-256 digest: *//p' | tr -d '\r')
RELEASE_CERT=$(with_signing sh -c '"$1" -list -v -keystore "$2" -alias "$3" -storepass:env GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD' \
	_ "$KEYTOOL" "$(cygpath -w "$KEYSTORE" 2>/dev/null || echo "$KEYSTORE")" "$KEY_ALIAS" \
	| sed -n 's/^[[:space:]]*SHA256: *//p' | tr -d ':\r' | tr 'A-F' 'a-f' || true)
if [ -z "$APK_CERT" ] || [ "$APK_CERT" != "$RELEASE_CERT" ]; then
	echo "$APK isn't signed with the release key (APK: ${APK_CERT:-none}, release key: ${RELEASE_CERT:-unreadable})" >&2
	exit 1
fi
if echo "$SIGNERS" | grep -qE "certificate DN: .*(Android Debug|Godot Engine)"; then echo "$APK carries a debug certificate" >&2; exit 1; fi
BADGING=$("$AAPT" dump badging "$APK" 2>/dev/null | sed -n 1p || true)
echo "$BADGING" | grep -q "versionCode='$VERSION_CODE' versionName='$VERSION'" || { echo "$APK has the wrong version: $BADGING" >&2; exit 1; }
# The app's identity: an APK with any other package id is a second app that can't update the first.
echo "$BADGING" | grep -q "^package: name='com.ecec.thefartherwestray' " || { echo "$APK isn't com.ecec.thefartherwestray: $BADGING" >&2; exit 1; }
cp "$APK" build/release/TheFartherWeStray-android.apk
printf '%-34s %6.1f MB  (versionCode %s)\n' TheFartherWeStray-android.apk "$(awk "BEGIN { print $(stat -c %s "$APK") / 1048576 }")" "$VERSION_CODE"
printf '%-34s %6.1f MB\n' TheFartherWeStray-Setup.exe "$(awk "BEGIN { print $(stat -c %s build/release/TheFartherWeStray-Setup.exe) / 1048576 }")"

echo "build/release/version.json:"
sed 's/^/    /' build/release/version.json

if $DRY_RUN; then
	echo "Dry run done: build/release/ holds every file v$VERSION would publish. Nothing was committed, tagged, pushed or released."
	exit 0
fi

git diff --quiet project.godot export_presets.cfg || git commit -q -m "Release v$VERSION" project.godot export_presets.cfg
COMMITTED=true
git tag "v$VERSION"
git push -q origin HEAD "v$VERSION"

gh release create "v$VERSION" --repo eclancy/TheFartherWeStray \
	build/release/TheFartherWeStray-Setup.exe build/release/TheFartherWeStray-windows.zip \
	build/release/TheFartherWeStray-patch.zip build/release/TheFartherWeStray-android.apk \
	build/release/version.json \
	--title "The Farther We Stray $VERSION" \
	--notes "${CHANGES:+$CHANGES

}$HOW_TO"

echo "Released v$VERSION: every Windows copy updates the next time it starts, and every Android copy offers it on its title."
