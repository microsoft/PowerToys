#define MyAppName "Wox"
#define MyAppPublisher "qianlifeng"
#define MyAppURL "http://www.getwox.com"
#define MyAppExeName "Wox.exe"
#define MyAppPath SourcePath + "..\..\Output\Release"
#define OutputPath SourcePath + "..\..\Output"
#define MyAppVer = GetFileVersion(MyAppPath + "\Wox.exe")

[Setup]
AppId={{A5AF4C34-70A7-4D3B-BA18-E49C0AEEA5E6}
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

[Languages]
Name: english; MessagesFile: compiler:Default.isl

[InstallDelete]
Type: files; Name: "{commonstartup}\{#MyAppName}.lnk"

[Tasks]
Name: desktopicon; Description: {cm:CreateDesktopIcon}; GroupDescription: {cm:AdditionalIcons}; Flags: unchecked
Name: startupfolder; Description: Startup with Windows;

[Files]
Source: {#MyAppPath}\*; DestDir: {app}; Flags: ignoreversion recursesubdirs

[Icons]
Name: {group}\{#MyAppName}; Filename: {app}\{#MyAppExeName}
Name: {group}\{cm:UninstallProgram,{#MyAppName}}; Filename: {uninstallexe}
Name: {userdesktop}\{#MyAppName}; Filename: {app}\{#MyAppExeName}; Tasks: desktopicon
Name: {userstartup}\{#MyAppName}; Filename: {app}\{#MyAppExeName}; Tasks: startupfolder

[Run]
Filename: {app}\{#MyAppExeName}; Description: {cm:LaunchProgram,{#MyAppName}}; Flags: nowait postinstall skipifsilent
