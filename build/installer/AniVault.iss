; Inno Setup script for AniVault.
; Compiled by build/make-installer.ps1 after build/publish.ps1 has produced the single-file exe.
;
; The installer is optional - the portable ZIP is the primary distribution method.
; It installs a per-user copy (no admin prompt) so the app folder stays writable and the
; portable "anivault.config.json" mechanism keeps working. User library data is NEVER placed
; here; the user chooses a data folder on first run.
;
; Deliberately NOT uninstallable: the install folder holds exactly what the ZIP holds
; (AniVault.exe + README.txt), with no unins000.* files and no "Installed apps" entry.
; Removing the app = deleting the folder and the shortcuts (README.txt says so).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\..\artifacts\publish\AniVault.exe"
#endif
#ifndef SourceReadme
  #define SourceReadme "..\..\artifacts\publish\README.txt"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

#define AppName "AniVault"
#define AppPublisher "AniVault"
#define AppExe "AniVault.exe"
; Must match AppUserModelId in src/AniVault/App.TaskbarIdentity.cs, so a pinned shortcut and
; the running window share one taskbar button.
#define AppUserModelId "AniVault.AniVault"

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
Uninstallable=no
OutputDir={#OutputDir}
OutputBaseFilename=AniVault-{#AppVersion}-Setup
SetupIconFile=..\..\src\AniVault\Resources\Icons\AniVault.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion
Source: "{#SourceReadme}"; DestDir: "{app}"; DestName: "README.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; --- Upgrading over a build from before 1.9.1, which still shipped an uninstaller ---
[InstallDelete]
Type: files; Name: "{app}\unins000.exe"
Type: files; Name: "{app}\unins000.dat"
Type: files; Name: "{group}\{cm:UninstallProgram,{#AppName}}.lnk"

[Registry]
; Drop the old "Installed apps" entry, which would otherwise point at the removed uninstaller.
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{9C2B7B1E-6E7C-4E2E-9E4B-6D3A1B7F5A20}_is1"; Flags: deletekey
