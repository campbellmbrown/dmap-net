; Inno Setup script for DMap.
;
; All external inputs are supplied as /D defines on the ISCC command line, using
; container-absolute /work/... paths when compiled via the amake/innosetup Docker
; image (see .github/workflows/release.yml). Required defines:
;   AppVersion   - release version, e.g. v1.3.0
;   SourceDir    - the published, self-contained win-x64 folder to package
;   LicenseFile  - path to the license shown in the wizard
;   SetupIconFile- path to the .ico used for the setup executable
;   OutputDir    - directory the setup.exe is written to
;   OutputName   - base filename (without extension) of the setup.exe

#ifndef AppVersion
  #define AppVersion "dev"
#endif

#define AppName "DMap"
#define AppPublisher "Campbell Brown"
#define AppExeName "DMap.exe"

[Setup]
; AppId uniquely identifies this application; it must NOT change between releases,
; otherwise upgrades and uninstall entries will not bind to the same product.
AppId={{7B2F4C1E-9A3D-4E6B-8F52-1C7D0A9E4B33}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LicenseFile={#LicenseFile}
SetupIconFile={#SetupIconFile}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
