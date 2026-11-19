; Instalador do Último Round. Gere pelo menu Último Round > Build > Instalador Windows.
; A versão vem de BuildTool.Version (/DAppVersion). Instala só para o usuário atual, sem pedir administrador.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{524B11CD-8C88-498D-BB9E-7A5FADCB9729}
AppName=Último Round
AppVersion={#AppVersion}
AppVerName=Último Round {#AppVersion}
AppPublisher=Ultimo Round Dev
DefaultDirName={autopf}\Último Round
DisableProgramGroupPage=yes
DefaultGroupName=Último Round
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\UltimoRound.exe
UninstallDisplayName=Último Round
OutputDir=..\Dist
OutputBaseFilename=UltimoRound_Setup_v{#AppVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\Dist\UltimoRound_Windows_x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Último Round"; Filename: "{app}\UltimoRound.exe"
Name: "{autodesktop}\Último Round"; Filename: "{app}\UltimoRound.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\UltimoRound.exe"; Description: "{cm:LaunchProgram,Último Round}"; Flags: nowait postinstall skipifsilent
