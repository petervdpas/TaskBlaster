#define AppName "TaskBlaster"
; AppVersion is injected at build time via: iscc /DAppVersion=x.y.z
#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#define AppPublisher "Peter van de Pas"
#define AppExeName "TaskBlaster.exe"
#define SetupIco "assets\app.ico"

[Setup]
AppId={{6DD336A8-3B19-4A36-A536-BAA5C8690070}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Installer EXE icon
SetupIconFile={#SetupIco}

OutputDir=output
OutputBaseFilename=TaskBlaster-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes

[Files]
; App payload
Source: "..\..\out\windows-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

; Ship the icon file so shortcuts can explicitly use it
Source: "{#SetupIco}"; DestDir: "{app}"; DestName: "app.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\app.ico"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
