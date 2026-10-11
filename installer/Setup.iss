#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef OutputDir
  #error OutputDir must be supplied
#endif
[Setup]
AppId={{FE8A4D03-02BB-5593-B3C8-F1851C02A8A3}
AppName=Cliptray
AppVersion={#AppVersion}
AppPublisher=informent
AppPublisherURL=https://github.com/informent/Cliptray
DefaultDirName={localappdata}\Programs\Cliptray
DefaultGroupName=Cliptray
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Cliptray-v{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Cliptray.exe
CloseApplications=yes
CloseApplicationsFilter=Cliptray.exe
RestartApplications=no
SetupLogging=yes
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Cliptray"; Filename: "{app}\Cliptray.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Cliptray"; Filename: "{app}\Cliptray.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Cliptray.exe"; Description: "Open Cliptray"; Flags: nowait postinstall skipifsilent unchecked
