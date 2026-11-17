; Inno Setup 6 script for Zaplio.
;
; 1) Publish a self-contained build (run from the repository root):
;      dotnet publish src\Zaplio -c Release -r win-x64 --self-contained true -o artifacts\Zaplio-0.11.0-win-x64
; 2) Compile this script with Inno Setup 6 (ISCC.exe installer\Zaplio.iss).
;    The installer is written to artifacts\Zaplio-0.11.0-setup.exe.
; Signing: publish with -p:ZaplioSign=true so every binary inside is signed, then sign the
; installer itself by uncommenting SignTool below and registering the tool in Inno Setup (Tools > Configure Sign Tools):
;   Name: zaplio   Command: powershell -NoProfile -ExecutionPolicy Bypass -File "<repository>\tools\Sign-Zaplio.ps1" -Path $f
; When releasing a new version, update AppVersion below together with <Version> in src\Zaplio\Zaplio.csproj,
; and tag the GitHub release as v<version> so the in-app update check can find it.

#define AppName "Zaplio"
; The release workflow passes /DAppVersion=<tag version>; local builds fall back to this value.
#ifndef AppVersion
  #define AppVersion "0.11.0"
#endif
#define PublishDir "..\artifacts\Zaplio-" + AppVersion + "-win-x64"

[Setup]
AppId={{C2FB3293-BBD3-4B83-9C3D-B0E6008461BB}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=Zaplio-{#AppVersion}-setup
SetupIconFile=..\src\Zaplio\Assets\Zaplio.ico
UninstallDisplayIcon={app}\Zaplio.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
;SignTool=zaplio
;SignedUninstaller=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Zaplio.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Zaplio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Zaplio.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; An update started from inside Zaplio runs silently; Zaplio is opened again when it is done.
Filename: "{app}\Zaplio.exe"; Parameters: "--updated"; Flags: nowait; Check: WizardSilent

; User data (%LocalAppData%\Zaplio: sources, cache, logs) is kept on uninstall on purpose.
