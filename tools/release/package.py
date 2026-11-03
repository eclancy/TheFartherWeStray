# -*- coding: utf-8 -*-
"""Turn the export in build/windows/ into the files a GitHub Release carries.

    python tools/release/package.py 0.3.0

Writes build/release/:

  TheFartherWeStray-windows.zip   everything, in a TheFartherWeStray/ folder: the portable version
                                  (the installer is what a new player downloads)
  TheFartherWeStray-patch.zip     just the game: the .pck and the game's own assembly. A fraction of
                                  the full zip, because the engine and the .NET runtime only change
                                  when Godot is upgraded.
  version.json                    what the game's update check reads (scripts/UI/UpdateScreen.cs and
                                  UpdateBanner.cs)

The installer (TheFartherWeStray-Setup.exe, tools/release/installer.iss) and the Android APK are made
by release.sh; version.json carries their addresses. Normally run by release.sh, not by hand. See
docs/releasing.md. Python 2.7, the Python on this machine; after The One He Missed's package.py.
"""
from __future__ import print_function

import io
import json
import os
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT = os.path.join(ROOT, "build", "windows")
OUT = os.path.join(ROOT, "build", "release")
REPO = "eclancy/TheFartherWeStray"
# Every file a release carries starts with this, and so do the program, its .pck and the .NET folder.
NAME = "TheFartherWeStray"
DATA = "data_%s_windows_x86_64" % NAME
FULL_ZIP = NAME + "-windows.zip"
PATCH_ZIP = NAME + "-patch.zip"
INSTALLER = NAME + "-Setup.exe"
APK = NAME + "-android.apk"

# The engine the export was made with: a patch is only offered to a copy on the same engine, and
# anything else downloads the full zip. Read from the editor's file name, so upgrading Godot can't
# leave it behind.
GODOT = os.environ.get("GODOT_BIN", "")

# What the patch carries: everything that changes when the game changes and the engine doesn't.
PATCH = [NAME + ".pck", DATA + "/" + NAME + ".dll", DATA + "/" + NAME + ".deps.json",
         DATA + "/" + NAME + ".runtimeconfig.json"]

HOW_TO_PLAY = u"""The Farther We Stray
====================

Catch strays, breed and train your beasts, and lead them in turn-based battles, from your
farmyard home out to a gilded city and the undercity beneath it.

1. Double-click TheFartherWeStray.exe. Keep the files beside it together: the game needs them.

2. The first time, Windows may say "Windows protected your PC". That's because this game isn't
   from a big company that paid for a signature. Click "More info", then "Run anyway".

3. Controls: the mouse does everything. On the keyboard, the arrows and Enter choose and pick,
   Esc goes back and opens the menu, WASD pans the battle camera, and N turns the sound off.

The game updates itself: every time it starts, it checks for a new version and installs it.
Keep this folder somewhere you can write to, like Documents or the Desktop, so it can.

Made at https://ecec.dev - download page: https://ecec.dev/the-farther-we-stray/download
"""


def export_files(src):
    """Every file of an export as (path on disk, path inside it with forward slashes), sorted."""
    found = []
    for dirpath, _dirs, files in os.walk(src):
        for f in sorted(files):
            full = os.path.join(dirpath, f)
            found.append((full, os.path.relpath(full, src).replace(os.sep, "/")))
    return found


def engine_version():
    name = os.path.basename(GODOT)
    # Godot_v4.5.1-stable_mono_win64_console.exe -> 4.5.1
    if name.startswith("Godot_v"):
        return name[len("Godot_v"):].split("-")[0]
    raise SystemExit("set GODOT_BIN to the Godot editor, so the engine version is known")


def main(argv):
    if not argv:
        print(__doc__)
        return 1
    version = argv[0]
    engine = engine_version()  # first, so a missing GODOT_BIN can't leave half a release behind
    for rel in PATCH + [NAME + ".exe"]:
        if not os.path.isfile(os.path.join(EXPORT, rel.replace("/", os.sep))):
            raise SystemExit("the export has no %s - run the Godot export first" % rel)
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    z = zipfile.ZipFile(os.path.join(OUT, FULL_ZIP), "w", zipfile.ZIP_DEFLATED, allowZip64=True)
    for path, rel in export_files(EXPORT):
        z.write(path, NAME + "/" + rel)
    z.writestr(NAME + "/How to play.txt", HOW_TO_PLAY.replace(u"\n", u"\r\n").encode("utf-8"))
    z.close()

    z = zipfile.ZipFile(os.path.join(OUT, PATCH_ZIP), "w", zipfile.ZIP_DEFLATED)
    for rel in PATCH:
        z.write(os.path.join(EXPORT, rel.replace("/", os.sep)), rel)
    z.close()

    base = "https://github.com/%s/releases/download/v%s/" % (REPO, version)
    info = {
        "version": version,
        "engine": engine,
        # What a Windows copy updates from: the patch on the same engine, the full zip otherwise.
        "builds": {"windows": {"patch": base + PATCH_ZIP, "full": base + FULL_ZIP}},
        # For anything that wants this release's installer by address; the game doesn't read it.
        "installer": base + INSTALLER,
        # The release-signed APK: the Android copy offers it in the browser (UpdateBanner).
        "android": base + APK,
    }
    with io.open(os.path.join(OUT, "version.json"), "w", encoding="utf-8") as f:
        f.write(unicode(json.dumps(info, indent=2, sort_keys=True)) + u"\n")

    for name in [FULL_ZIP, PATCH_ZIP, "version.json"]:
        print("%-34s %6.1f MB" % (name, os.path.getsize(os.path.join(OUT, name)) / 1048576.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
