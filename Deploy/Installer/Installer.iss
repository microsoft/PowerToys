#define MyAppName "Wox"
#define MyAppPublisher "qianlifeng"
#define MyAppURL "http://www.getwox.com"
#define MyAppExeName "Wox.exe"
#define MyAppPath SourcePath + "..\..\Output\Release"
#define OutputPath SourcePath + "..\..\Output"
#define MyAppVer = GetFileVersion(MyAppPath + "\Wox.exe")

[Setup]
AppId=05700E94-3DAD-4827-8AAA-9908178DE132-Wox
AppMutex=DBDE24E4-91F6-11DF-B495-C536DFD72085-Wox
AppName={#MyAppName}
AppVerName={#MyAppName} v{#MyAppVer}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=Wox-setup
OutputDir={#OutputPath}
Compression=lzma
SolidCompression=yes
DisableDirPage=auto
DisableProgramGroupPage=auto

[Languages]
Name: english; MessagesFile: compiler:Default.isl

[InstallDelete]
Type: files; Name: "{commonstartup}\{#MyAppName}.lnk"

[Tasks]
Name: desktopicon; Description: {cm:CreateDesktopIcon}; GroupDescription: {cm:AdditionalIcons};
Name: startupfolder; Description: Startup with Windows;

[Files]
Source: {#MyAppPath}\*; Excludes: Plugins\*; DestDir: {app}; Flags: ignoreversion recursesubdirs
Source: {#MyAppPath}\Plugins\*; DestDir: {%USERPROFILE}\.Wox\Plugins; Flags: ignoreversion recursesubdirs
Source: {#MyAppPath}\Themes\Base.xaml; DestDir: {%USERPROFILE}\.Wox\Themes; Flags: ignoreversion recursesubdirs

[Icons]
Name: {group}\{#MyAppName}; Filename: {app}\{#MyAppExeName}
Name: {group}\{cm:UninstallProgram,{#MyAppName}}; Filename: {uninstallexe}
Name: {userdesktop}\{#MyAppName}; Filename: {app}\{#MyAppExeName}; Tasks: desktopicon
Name: {userstartup}\{#MyAppName}; Filename: {app}\{#MyAppExeName}; Tasks: startupfolder

[Run]
Filename: {app}\{#MyAppExeName}; Description: {cm:LaunchProgram,{#MyAppName}}; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: filesandordirs; Name: "{%USERPROFILE}\.Wox"

[UninstallRun]
Filename: {sys}\taskkill.exe; Parameters: "/f /im Wox.exe"; Flags: skipifdoesntexist runhidden
