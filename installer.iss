#define MyAppName "HOCO KeepAlive"
#define MyAppVersion "1.0.0"
#define MyPublisher "Raiyan"
#define MyExeName "HOCO KeepAlive.exe"

[Setup]
AppId={{9E6F1B51-3B7E-4B9B-9B21-4A1CCF77A210}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={autopf}\HOCO KeepAlive
DefaultGroupName={#MyAppName}
OutputDir=installer-output
OutputBaseFilename=HOCO-KeepAlive-Setup-1.0.0
SetupIconFile=assets\hoco-keepalive.ico
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyExeName}

[Tasks]
Name: "startup"; Description: "Start HOCO KeepAlive when I sign in to Windows"; GroupDescription: "Optional:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\HOCO KeepAlive"; Filename: "{app}\{#MyExeName}"; IconFilename: "{app}\assets\hoco-keepalive.ico"
Name: "{autodesktop}\HOCO KeepAlive"; Filename: "{app}\{#MyExeName}"; IconFilename: "{app}\assets\hoco-keepalive.ico"; Tasks: startup

[Run]
Filename: "{app}\{#MyExeName}"; Description: "Launch HOCO KeepAlive"; Flags: nowait postinstall skipifsilent
