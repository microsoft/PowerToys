; TEMPLATE: Inno Setup Script for Command Palette Extensions
;
; To use this template for a new extension:
; 1. Copy this file to your extension's project folder as "setup-template.iss"
; 2. Replace EXTENSION_NAME with your extension name (e.g., CmdPalMyExtension)
; 3. Replace DISPLAY_NAME with your extension's display name (e.g., My Extension)
; 4. Replace DEVELOPER_NAME with your name (e.g., Your Name Here)
; 5. Replace CLSID-HERE with extensions CLSID
; 6. Update the default version to match your project file

#define AppVersion "0.0.1.0"

[Setup]
AppId={{GUID-HERE}}
AppName=DISPLAY_NAME
AppVersion={#AppVersion}
AppPublisher=DEVELOPER_NAME
DefaultDirName={autopf}\EXTENSION_NAME
OutputDir=bin\Release\installer
OutputBaseFilename=EXTENSION_NAME-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\DISPLAY_NAME"; Filename: "{app}\EXTENSION_NAME.exe"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{CLSID-HERE}}"; ValueData: "EXTENSION_NAME"
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{CLSID-HERE}}\LocalServer32"; ValueData: "{app}\EXTENSION_NAME.exe -RegisterProcessAsComServer"