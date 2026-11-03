#!/usr/bin/env bash
# Build The Farther We Stray for Android: a release-signed APK at build/android/TheFartherWeStray.apk.
#
#   tools/android-build.sh            export and check the APK
#   tools/android-build.sh --install  then install it on the phone adb sees (and start it)
#
# Needs $GODOT_BIN (the console exe; see .claude/settings.json), Godot's 4.5.1 mono export templates,
# and the Android SDK and JDK 17 that Godot's editor settings point at. The signing key is
# %APPDATA%/Godot/keystores/thefartherwestray-release.keystore, its password beside it in
# thefartherwestray-release.secret (alias=..., password=...): never in this repo. Back both up:
# Android only installs an update signed with the same key, so a lost key strands every copy out there.
#
# The password goes to Godot through the environment of the export alone, never on a command line.
# Godot's headless export can finish the APK and then not exit, so it's judged by the file it writes.
set -u
cd "$(dirname "$0")/.." || exit 1

KEYSTORE="${APPDATA:-$HOME}/Godot/keystores/thefartherwestray-release.keystore"
SECRET="${KEYSTORE%.keystore}.secret"
APK="build/android/TheFartherWeStray.apk"
SETTINGS="${APPDATA:-$HOME}/Godot/editor_settings-4.5.tres"

setting() { grep "^export/android/$1 = " "$SETTINGS" 2>/dev/null | sed 's/.* = "\(.*\)"/\1/'; }
SDK="${ANDROID_SDK_ROOT:-$(setting android_sdk_path)}"
JDK="${JAVA_HOME:-$(setting java_sdk_path)}"
APKSIGNER="$(ls -d "$SDK"/build-tools/*/ 2>/dev/null | sort -V | tail -1)apksigner.bat"
[ -f "$APKSIGNER" ] || APKSIGNER="${APKSIGNER%.bat}"

for need in "${GODOT_BIN:-}" "$KEYSTORE" "$SECRET" "$APKSIGNER"; do
	if [ -z "$need" ] || [ ! -e "$need" ]; then echo "Missing: ${need:-GODOT_BIN}" >&2; exit 1; fi
done

mkdir -p build/android
rm -f "$APK"
echo "Exporting (a cold export takes a few minutes) ..."
(
	export GODOT_ANDROID_KEYSTORE_RELEASE_PATH="$KEYSTORE"
	export GODOT_ANDROID_KEYSTORE_RELEASE_USER="$(sed -n 's/^alias=//p' "$SECRET")"
	export GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD="$(sed -n 's/^password=//p' "$SECRET")"
	timeout 1800 "$GODOT_BIN" --headless --path . --export-release "Android" "$APK" > build/android/export.log 2>&1
)
if [ ! -s "$APK" ]; then
	echo "No APK was written. The end of build/android/export.log:" >&2
	tail -30 build/android/export.log >&2
	exit 1
fi
if grep -qF "$(sed -n 's/^password=//p' "$SECRET")" build/android/export.log; then
	rm -f build/android/export.log
	echo "The export log held the password; deleted it. Not using this APK." >&2
	exit 1
fi

echo "Checking the signature ..."
JAVA_HOME="$JDK" "$APKSIGNER" verify --print-certs "$APK" | grep -E "Signer #1 certificate (DN|SHA-256)" || { echo "The APK isn't signed." >&2; exit 1; }
echo "Built $APK ($(( $(stat -c %s "$APK") / 1048576 )) MB)"

if [ "${1:-}" = "--install" ]; then
	ADB="$SDK/platform-tools/adb"
	"$ADB" install -r "$APK" && "$ADB" shell monkey -p com.ecec.thefartherwestray -c android.intent.category.LAUNCHER 1 > /dev/null
fi
