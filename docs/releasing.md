# Releasing

How a downloadable copy of The Farther We Stray gets to people, and how it keeps itself up to date
(the user's ask, 2026-11-03: the way The One He Missed does it; ported from its `.ai/releasing.md`
and code, without its rename bridges, story epochs or Linux build).

**In one line:** `bash tools/release/release.sh X.Y.Z "What changed"` makes a GitHub Release. The
next time a Windows copy starts, it finds the release and updates itself; an Android copy says on
its title that the new version is out and offers the APK.

## Where things live

| What | Where |
|---|---|
| The download page | `ecec.dev/the-farther-we-stray/download` (EricClancyEngineeringAndCrafts repo): the installer, the portable zip, the APK |
| The game files | GitHub Releases on `eclancy/TheFartherWeStray` (public): free, no bandwidth cap |
| The update check | `ecec.dev/the-farther-we-stray/version.json`: an nginx 302 to `github.com/eclancy/TheFartherWeStray/releases/latest/download/version.json` |

The update address is on ecec.dev because it's the one address compiled into every copy
(`UpdateScreen.VersionUrl`): behind a redirect, the releases can move later without stranding old
copies. The page links to GitHub's `releases/latest/download/...`, so neither the page nor ecec.dev
needs a deploy for a release. Copies from before 0.3.0 have no update check: their players
download 0.3.0 once by hand.

## What a release carries

- **`TheFartherWeStray-Setup.exe`**: the installer, what a new Windows player downloads (Inno
  Setup, `tools/release/installer.iss`). Per user, to `%LOCALAPPDATA%\Programs\TheFartherWeStray`,
  no admin prompt, because the updater copies files into the game's own folder and Program Files
  would leave it stuck. Start menu and desktop shortcuts. **Its `AppId` GUID must never change**: it's
  how a newer setup upgrades the same program in place. Uninstalling leaves the saves
  (`%APPDATA%\Godot\app_userdata\The Farther We Stray\`).
- **`TheFartherWeStray-windows.zip`**: the portable version: the export in a `TheFartherWeStray/`
  folder with `How to play.txt`. It updates itself the same way.
- **`TheFartherWeStray-patch.zip`**: the `.pck` and the game's DLL with its two JSON files: all
  that changes when the game changes and Godot doesn't. What a copy on the same engine downloads.
- **`TheFartherWeStray-android.apk`**: release-signed, version code `major*10000 + minor*100 +
  patch`, package `com.ecec.thefartherwestray` (never change it).
- **`version.json`**: the version, the engine it was built with, `builds.windows` (`patch` and
  `full`), `installer` and `android`. Adding a field is safe: the game reads only the ones it names.

`tools/release/package.py` (Python 2.7) makes the zips and `version.json`, ISCC the installer,
`release.sh` exports both platforms and checks the APK: verified by apksigner, signed with the
release key (its SHA-256 compared with the keystore's through keytool), no debug certificate, the
right version code, name and package. It reads the keystore and password as
`tools/android-build.sh` does (`%APPDATA%\Godot\keystores\thefartherwestray-release.*`), into the
export's environment alone.

## How a copy updates

**Windows** (`scripts/UI/UpdateScreen.cs`): an exported Windows build only (`template` feature),
once per launch, laid over the title before its menu takes input. It fetches `version.json` (5 s
timeout), compares versions number by number, takes the patch if the release was built on this
copy's Godot, else the full zip, unpacks to `user://update/`, writes `apply.cmd`, starts it and
quits; the script waits for the game to close, robocopies the new files over the old and starts it
again. **Anything that goes wrong just goes on to the title: an update is never worth not being
able to play.** The version shows small in the title's corner.

**Android** (`scripts/UI/UpdateBanner.cs`): an app can't install an APK without asking for
`REQUEST_INSTALL_PACKAGES`, which makes a sideloaded game look like malware, so it only tells the
player: a strip along the foot of the title, "Version X is out", **Download** (opens the APK in the
browser) and **Later** (until the next launch). It never holds the title up, and its buttons are
tap-only (`PadNav.Skip`), so the pad's A on the title only does what the menu says. The browser
downloads, Android's installer asks, and the same signature and a higher version code install it
over the old one, saves and all.

`--no-update` turns both off. `--update-screen=9.9.9` and `--update-banner=9.9.9` show them in any
build without the network (screenshots). `THEFARTHERWESTRAY_UPDATE_URL` points the check at a
test server.

## The shader cache is off

Found 2026-11-03, testing the updater: an exported build crashed (a segfault after `shader_gles3.cpp:800`
errors) **every second time it started**, here on Intel Iris Plus graphics: the GL shader cache the
first run wrote, the second couldn't load. Runs from the editor never did it. So `project.godot`
turns it off (`rendering/shader_compiler/shader_cache/enabled=false`); the cost is compiling the
game's few 2D shaders at start. Keep it off unless a later Godot fixes it, and test two launches in
a row of an exported build before turning it back on. Copies of 0.2.0 still have the cache on and no
updater: their players need 0.3.0 from the download page once.

## Making a release

```sh
bash tools/release/release.sh 0.3.0 "What changed, for players"
```

Needs a clean tree (docs aside: no build has them), Inno Setup (`winget install
JRSoftware.InnoSetup`), the keystore, and the Android SDK and JDK the Godot editor settings name.
It bumps the version and version code, exports, packages, compiles the installer, exports and
checks the APK, commits "Release vX.Y.Z", tags, pushes, and runs `gh release create`.
`--dry-run` does everything but the commit, tag, push and release, then puts the version back.

**Testing an update without publishing:** dry-run the new version; export an older copy
(`config/version` set lower) to `build/test/`; serve a `version.json` whose URLs point at the dry
run's zips on a local server; run the old copy with `THEFARTHERWESTRAY_UPDATE_URL` at it and
`APPDATA` at a scratch folder (so no real save is touched). It should download, restart, and come
back as the new version, its `.pck` byte-identical to the new export's.
