#define MyAppName "Keep Alive"
#define MyAppVersion "1.0.4"
#define MyPublisher "Raiyan"
#define MyExeName "Keep Alive.exe"

[Setup]
AppId={{9E6F1B51-3B7E-4B9B-9B21-4A1CCF77A210}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={autopf}\Keep Alive
DefaultGroupName={#MyAppName}
OutputDir=installer-output
OutputBaseFilename=Keep-Alive-Setup-{#MyAppVersion}
SetupIconFile=assets\hoco-keepalive.ico
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Keep Alive when I sign in to Windows"; GroupDescription: "Windows Startup:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"; IconFilename: "{app}\{#MyExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"; IconFilename: "{app}\{#MyExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Keep Alive"; ValueData: """{app}\{#MyExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
