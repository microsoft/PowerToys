# Settings resource
Manage the settings for PowerToys modules

## Commands

### ✨ Modules
List all the modules supported by the settings resource.
```shell
PS C:\> PowerToys.DSC.exe modules --resource 'settings'
AdvancedPaste
AlwaysOnTop
App
Awake
ColorPicker
CropAndLock
EnvironmentVariables
FancyZones
FileLocksmith
FindMyMouse
Hosts
ImageResizer
KeyboardManager
MeasureTool
MouseHighlighter
MouseJump
MousePointerCrosshairs
Peek
PowerAccent
PowerOCR
PowerRename
RegistryPreview
ShortcutGuide
Workspaces
ZoomIt
```

### 📄 Get
Get the settings for a specific module.
```shell
PS C:\> PowerToys.DSC.exe get --resource 'settings' --module EnvironmentVariables
{"settings":{"properties":{"LaunchAdministrator":{"value":true}},"name":"EnvironmentVariables","version":"1.0"}}
```

### 🖨️ Export
Export the settings for a specific module.

ℹ️ Settings resource Get and Export operation output states are identical.
```shell
PS C:\> PowerToys.DSC.exe get --resource 'settings' --module EnvironmentVariables
{"settings":{"properties":{"LaunchAdministrator":{"value":true}},"name":"EnvironmentVariables","version":"1.0"}}
```

### 📝 Set
Set the settings for a specific module. This command will update the settings to the specified values.
```shell
PS C:\> PowerToys.DSC.exe set --resource 'settings' --module Awake --input '{"settings":{"properties":{"keepDisplayOn":false,"mode":0,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2025-08-13T10:10:00.000001-07:00","customTrayTimes":{}},"name":"Awake","version":"0.0.1"}}'
{"settings":{"properties":{"keepDisplayOn":false,"mode":0,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2025-08-13T10:10:00.000001-07:00","customTrayTimes":{}},"name":"Awake","version":"0.0.1"}}
["settings"]
```

### 🧪 Test
Test the settings for a specific module. This command will check if the current settings match the desired state.
```shell
PS C:\> PowerToys.DSC.exe test --resource 'settings' --module Awake --input '{"settings":{"properties":{"keepDisplayOn":false,"mode":0,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2025-08-13T10:10:00.000002-07:00","customTrayTimes":{}},"name":"Awake","version":"0.0.1"}}'
{"settings":{"properties":{"keepDisplayOn":false,"mode":0,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2025-08-13T10:10:00.000001-07:00","customTrayTimes":{}},"name":"Awake","version":"0.0.1"},"_inDesiredState":false}
["settings"]
```

### 🛠️ Schema
Generates the JSON schema for the settings resource of a specific module.
```shell
PS C:\> PowerToys.DSC.exe schema --resource 'settings' --module Awake
{"$schema":"http://json-schema.org/draft-04/schema#","title":"SettingsResourceObjectOfAwakeSettings","type":"object","additionalProperties":false,"required":["settings"],"properties":{"_inDesiredState":{"type":["boolean","null"],"description":"Indicates whether an instance is in the desired state"},"settings":{"description":"The settings content for the module."}}}
PS E:\src\powertoys> PowerToys.DSC.exe schema --resource 'settings' --module Awake | Format-Json
```

### 📦 Manifest
Generates a manifest dsc resource JSON file for the specified module.
- If the module is not specified, it will generate a manifest for all modules.
- If the output directory is not specified, it will print the manifest to the console.
```shell
PS C:\> PowerToys.DSC.exe manifest --resource settings --module 'Awake' --outputDir "C:\manifests"
```