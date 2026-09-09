; Inno Setup script for AniVault.
; Compiled by build/make-installer.ps1 after build/publish.ps1 has produced the single-file exe.
;
; The installer is optional - the portable ZIP is the primary distribution method.
; It installs a per-user copy (no admin prompt) so the app folder stays writable and the
; portable "anivault.config.json" mechanism keeps working. User library data is NEVER placed
; here; the user chooses a data folder on first run and it is left untouched on uninstall.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\..\artifacts\publish\AniVault.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

#define AppName "AniVault"
#define AppPublisher "AniVault"
#define AppExe "AniVault.exe"

[Setup]
AppId={{9C2B7B1E-6E7C-4E2E-9E4B-6D3A1B7F5A20}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=AniVault-{#AppVersion}-Setup
SetupIconFile=..\..\src\AniVault\Resources\Icons\AniVault.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove the portable pointer file the app may have written next to the exe.
Type: files; Name: "{app}\anivault.config.json"
