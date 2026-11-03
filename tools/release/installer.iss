; The Farther We Stray's Windows installer (Inno Setup 6), after The One He Missed's. Built by
; tools/release/release.sh:
;
;   ISCC.exe /DAppVersion=0.3.0 tools\release\installer.iss
;
; Packs the export in build\windows\ into build\release\TheFartherWeStray-Setup.exe.
;
; Per user, no admin prompt: it installs to %LOCALAPPDATA%\Programs\TheFartherWeStray, which the
; player can write to, and must, because the game updates itself by copying new files into its own
; folder (scripts/UI/UpdateScreen.cs). Program Files would leave it stuck on its first version.
; See docs/releasing.md.

#ifndef AppVersion
  #error Pass the version: ISCC /DAppVersion=X.Y.Z tools\release\installer.iss
#endif

[Setup]
; Never change this GUID: it's how Windows knows a newer setup is the same program, so it upgrades
; in place instead of installing a second copy.
AppId={{58367791-74A6-4043-9324-B491D72512FB}
AppName=The Farther We Stray
AppVersion={#AppVersion}
UninstallDisplayName=The Farther We Stray
AppPublisher=ECEC
AppPublisherURL=https://ecec.dev/the-farther-we-stray/download
; Paths below are relative to the repo root.
SourceDir=..\..
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\TheFartherWeStray
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
WizardStyle=modern
SetupIconFile=icon\icon.ico
UninstallDisplayIcon={app}\TheFartherWeStray.exe
OutputDir=build\release
OutputBaseFilename=TheFartherWeStray-Setup
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The export only. "How to play.txt" is written into the zip by package.py, never into
; build\windows, so it isn't picked up here.
Source: "build\windows\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\The Farther We Stray"; Filename: "{app}\TheFartherWeStray.exe"
Name: "{autodesktop}\The Farther We Stray"; Filename: "{app}\TheFartherWeStray.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TheFartherWeStray.exe"; Description: "Play The Farther We Stray now"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; The whole folder, files the updater added included. The saves live in
; %APPDATA%\Godot\app_userdata\The Farther We Stray\ and are left alone.
Type: filesandordirs; Name: "{app}"
