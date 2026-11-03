; Inno Setup 6 — інсталятор Buldog Boost. Збірка: iscc installer\BuldogBoost.iss (після scripts\build.ps1)
#define AppName "Buldog Boost"
#define AppVersion "0.2.0"
#define AppExe "BuldogBoost.exe"

[Setup]
AppId={{B7C2A5D1-4E3F-4A6B-9C8D-1F2E3A4B5C6D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Buldog Boost
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=BuldogBoost-Setup-{#AppVersion}
SetupIconFile=..\gui-csharp\assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "Створити ярлик на робочому столі"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Запустити {#AppName}"; Flags: nowait postinstall skipifsilent
