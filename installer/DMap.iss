; Inno Setup script for DMap.
;
; Compiled in CI via the amake/innosetup Docker image (Inno Setup under Wine) on the
; ubuntu-latest runner; see .github/workflows/release.yml. The workflow mounts the repo
; at /work and passes only the release version:
;   /DAppVersion=<tag>   e.g. v1.3.0
; All file paths below are relative to this script's own directory (installer/), which
; resolves correctly under both Wine and native Windows ISCC.

#ifndef AppVersion
  #define AppVersion "dev"
#endif

#define AppName "DMap"
#define AppPublisher "Campbell Brown"
#define AppURL "https://github.com/campbellmbrown/dmap-net"
#define AppExeName "DMap.exe"

[Setup]
; AppId uniquely identifies this application; it must NOT change between releases,
; otherwise upgrades and uninstall entries will not bind to the same product.
AppId={{16D59015-AA4E-448B-8239-B05BAE601E29}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; Install per-user by default (no forced UAC); logs live in %LOCALAPPDATA%\DMap.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LicenseFile=..\LICENSE
SetupIconFile=..\DMap\Assets\avalonia-logo.ico
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\release
OutputBaseFilename=DMap-{#AppVersion}-win-x64-setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Package the entire self-contained win-x64 publish folder.
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
