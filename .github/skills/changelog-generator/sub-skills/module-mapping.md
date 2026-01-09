# PowerToys Module Path Mapping

This sub-skill maps file paths to PowerToys module names for categorization.

## Module Path Mapping Table

| Module | Path Pattern |
|--------|--------------|
| Advanced Paste | `src/modules/AdvancedPaste/**` |
| Always On Top | `src/modules/alwaysontop/**` |
| Awake | `src/modules/Awake/**` |
| Color Picker | `src/modules/colorPicker/**` |
| Command Palette | `src/modules/cmdpal/**` |
| Crop And Lock | `src/modules/CropAndLock/**` |
| Environment Variables | `src/modules/EnvironmentVariables/**` |
| FancyZones | `src/modules/fancyzones/**` |
| File Explorer Add-ons | `src/modules/previewpane/**`, `src/modules/FileExplorerPreview/**` |
| File Locksmith | `src/modules/FileLocksmith/**` |
| Find My Mouse | `src/modules/MouseUtils/FindMyMouse/**` |
| Hosts File Editor | `src/modules/Hosts/**` |
| Image Resizer | `src/modules/imageresizer/**` |
| Keyboard Manager | `src/modules/keyboardmanager/**` |
| Light Switch | `src/modules/LightSwitch/**` |
| Mouse Highlighter | `src/modules/MouseUtils/MouseHighlighter/**` |
| Mouse Jump | `src/modules/MouseUtils/MouseJump/**` |
| Mouse Pointer Crosshairs | `src/modules/MouseUtils/MousePointerCrosshairs/**` |
| Mouse Without Borders | `src/modules/MouseWithoutBorders/**` |
| New+ | `src/modules/NewPlus/**` |
| Paste As Plain Text | `src/modules/PastePlain/**` |
| Peek | `src/modules/Peek/**` |
| PowerRename | `src/modules/powerrename/**` |
| PowerToys Run | `src/modules/launcher/**` |
| Quick Accent | `src/modules/QuickAccent/**` |
| Registry Preview | `src/modules/RegistryPreview/**` |
| Screen Ruler | `src/modules/MeasureTool/**` |
| Shortcut Guide | `src/modules/ShortcutGuide/**` |
| Text Extractor | `src/modules/TextExtractor/**` |
| Video Conference Mute | `src/modules/videoconference/**` |
| Workspaces | `src/modules/Workspaces/**` |
| ZoomIt | `src/modules/ZoomIt/**` |
| Settings | `src/settings-ui/**` |
| Runner | `src/runner/**` |
| Installer | `installer/**` |
| General / Infrastructure | `src/common/**`, `.github/**`, `tools/**` |

## Categorization by PR Labels

Common PowerToys PR labels for modules:
- `Product-FancyZones`
- `Product-PowerToys Run`
- `Product-Awake`
- `Product-ColorPicker`
- `Product-Keyboard Manager`
- etc.

## Auto-Categorization Script

```powershell
function Get-ModuleFromPath {
    param([string]$filePath)
    
    $moduleMap = @{
        'src/modules/AdvancedPaste/' = 'Advanced Paste'
        'src/modules/alwaysontop/' = 'Always On Top'
        'src/modules/Awake/' = 'Awake'
        'src/modules/colorPicker/' = 'Color Picker'
        'src/modules/cmdpal/' = 'Command Palette'
        'src/modules/CropAndLock/' = 'Crop And Lock'
        'src/modules/EnvironmentVariables/' = 'Environment Variables'
        'src/modules/fancyzones/' = 'FancyZones'
        'src/modules/previewpane/' = 'File Explorer Add-ons'
        'src/modules/FileExplorerPreview/' = 'File Explorer Add-ons'
        'src/modules/FileLocksmith/' = 'File Locksmith'
        'src/modules/MouseUtils/FindMyMouse/' = 'Find My Mouse'
        'src/modules/Hosts/' = 'Hosts File Editor'
        'src/modules/imageresizer/' = 'Image Resizer'
        'src/modules/keyboardmanager/' = 'Keyboard Manager'
        'src/modules/LightSwitch/' = 'Light Switch'
        'src/modules/MouseUtils/MouseHighlighter/' = 'Mouse Highlighter'
        'src/modules/MouseUtils/MouseJump/' = 'Mouse Jump'
        'src/modules/MouseUtils/MousePointerCrosshairs/' = 'Mouse Pointer Crosshairs'
        'src/modules/MouseWithoutBorders/' = 'Mouse Without Borders'
        'src/modules/NewPlus/' = 'New+'
        'src/modules/PastePlain/' = 'Paste As Plain Text'
        'src/modules/Peek/' = 'Peek'
        'src/modules/powerrename/' = 'PowerRename'
        'src/modules/launcher/' = 'PowerToys Run'
        'src/modules/QuickAccent/' = 'Quick Accent'
        'src/modules/RegistryPreview/' = 'Registry Preview'
        'src/modules/MeasureTool/' = 'Screen Ruler'
        'src/modules/ShortcutGuide/' = 'Shortcut Guide'
        'src/modules/TextExtractor/' = 'Text Extractor'
        'src/modules/videoconference/' = 'Video Conference Mute'
        'src/modules/Workspaces/' = 'Workspaces'
        'src/modules/ZoomIt/' = 'ZoomIt'
        'src/settings-ui/' = 'Settings'
        'src/runner/' = 'Runner'
        'installer/' = 'Installer'
        'src/common/' = 'General'
        '.github/' = 'Development'
        'tools/' = 'Development'
    }
    
    foreach ($pattern in $moduleMap.Keys) {
        if ($filePath -like "*$pattern*") {
            return $moduleMap[$pattern]
        }
    }
    return 'General'
}

# Usage: categorize a PR by its changed files
$files = gh pr view 12345 --repo microsoft/PowerToys --json files --jq '.files[].path'
$modules = $files | ForEach-Object { Get-ModuleFromPath $_ } | Sort-Object -Unique
Write-Host "PR affects modules: $($modules -join ', ')"
```

## Output Organization

Modules should be listed in **alphabetical order** in the changelog:

```markdown
### Advanced Paste
 - ...

### Awake
 - ...

### Command Palette
 - ...

### General
 - ...

### Development
 - ...
```
