---
title: Configure PowerToys with Desired State Configuration
description: >-
  Learn about the two approaches to configure PowerToys using Desired State
  Configuration: PowerShell DSC for legacy automation and Microsoft DSC for
  modern declarative configuration.
ms.date: 10/19/2025
ms.topic: overview
no-loc: [PowerToys, Windows, DSC, WinGet]
# customer intent: As a Windows power user or IT administrator, I want to
# understand the available DSC options for PowerToys configuration.
---

# Configure PowerToys with Desired State Configuration

PowerToys supports two Desired State Configuration (DSC) approaches for
automating and managing PowerToys settings across Windows systems. Each
approach serves different use cases and is designed for specific workflows.

## Understanding DSC in PowerToys

Desired State Configuration enables you to define, deploy, and enforce
configuration settings declaratively. Instead of writing scripts that perform
configuration steps, you describe the desired end state, and DSC ensures the
system matches that state.

PowerToys offers two distinct DSC implementations:

1. **PowerShell DSC (PSDSC)** - A legacy PowerShell-based module using DSC v2
2. **Microsoft DSC** - A modern, cross-platform implementation using DSC v3

> [!NOTE]
> These are two separate implementations with different capabilities, syntaxes, and use cases. Choose the approach that best fits your infrastructure and requirements.

## Choosing the right approach

### Use PowerShell DSC when

- You have existing PowerShell DSC infrastructure and workflows.
- You're using PowerShell 7.4 or higher with PSDesiredStateConfiguration 2.0.7+.
- You prefer PowerShell-based configuration management.

**Available since:** PowerToys v0.80.0

### Use Microsoft DSC when

- You need standalone PowerToys configuration without PowerShell dependencies.
- You're adopting Microsoft DSC standards and tooling.
- You want direct command-line configuration with `PowerToys.DSC.exe`.
- You need JSON schema validation and manifest generation.
- You're building automation with the latest Microsoft DSC ecosystem.

**Available since:** PowerToys v0.95.0

## Key differences

| Feature                  | PowerShell DSC                                      | Microsoft DSC                                                                                                                                    |
|--------------------------|-----------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| **DSC Version**          | v2                                                  | v3                                                                                                                                               |
| **Prerequisites**        | PowerShell 7.2+, PSDesiredStateConfiguration 2.0.7+ | None (standalone)                                                                                                                                |
| **Module Name**          | `Microsoft.PowerToys.Configure`                     | Resource types under `Microsoft.PowerToys/`                                                                                                      |
| **Command-line tool**    | PowerShell cmdlets                                  | `PowerToys.DSC.exe`                                                                                                                              |
| **Configuration format** | YAML (WinGet configuration)                         | YAML (Microsoft DSC configuration documents), JSON or Bicep JSON                                                                                 |
| **Resource model**       | Single `PowerToysConfigure` resource                | One settings resource per utility (for example, `AwakeSettings`), plus data resources such as `KeyboardManagerProfile` and `FancyZonesLayouts` |
| **Platform support**     | Windows only                                        | Cross-platform ready                                                                                                                             |
| **Schema support**       | Limited                                             | Full JSON schema generation                                                                                                                      |
| **Manifest generation**  | No                                                  | Yes                                                                                                                                              |

> [!IMPORTANT]
> While Microsoft DSC is cross-platform ready and can run on Windows, Linux, and macOS, PowerToys itself is a Windows-only application. The Microsoft DSC implementation provides a modern, cross-platform architecture that aligns with the broader DSC v3 ecosystem, but PowerToys configuration can only be applied on Windows systems where PowerToys is installed.

## Configuration scope

Both approaches manage the settings that you can change in the PowerToys Settings app, such as whether a utility is enabled, its activation shortcuts, and its behavior and appearance options. The approaches differ in which utilities they cover and in whether they can manage the data a utility stores outside of its settings.

### Utility settings

Both approaches manage the settings of the following utilities:

- General application settings (startup, theme, updates, enabled utilities)
- Advanced Paste
- Always On Top
- Awake
- Color Picker
- Crop And Lock
- Environment Variables
- FancyZones
- File Locksmith
- Find My Mouse
- Hosts File Editor
- Image Resizer
- Keyboard Manager
- Mouse Highlighter
- Mouse Jump
- Mouse Pointer Crosshairs
- Peek
- PowerRename
- Quick Accent (PowerAccent)
- Registry Preview
- Screen Ruler (MeasureTool)
- Shortcut Guide
- Text Extractor (PowerOCR)
- Workspaces
- ZoomIt

PowerShell DSC also manages the settings of utilities that Microsoft DSC doesn't expose yet, such as Alt Window Cycle, Cursor Wrap, Grab And Move, Light Switch, and the File Explorer add-ons. Individual settings that aren't suitable for automation are skipped by both approaches.

> [!NOTE]
> Microsoft DSC intentionally excludes Mouse Without Borders, PowerToys Run, and New+. Their settings contain a connection security key or absolute file paths, which aren't portable between systems.

### Utility data

Some utilities store their configuration in dedicated files rather than in their settings. Microsoft DSC provides resources that manage this data declaratively. PowerShell DSC manages settings only.

| Utility          | Data                                                                  | Microsoft DSC resource                       |
|------------------|-----------------------------------------------------------------------|----------------------------------------------|
| Keyboard Manager | Key remappings and shortcut remappings                                | `Microsoft.PowerToys/KeyboardManagerProfile` |
| FancyZones       | Custom layouts, layout templates, layout hotkeys, and default layouts | `Microsoft.PowerToys/FancyZonesLayouts`      |

When these resources are applied while PowerToys is running, the utility picks up the change immediately. Otherwise, the change takes effect the next time PowerToys starts.

## Next steps

Choose your DSC approach and get started:

- **[Configure with PowerShell DSC][01]** - Learn about the PowerShell-based DSC module and WinGet configuration integration
- **[Configure with Microsoft DSC][02]** - Explore the modern Microsoft DSC implementation with PowerToys.DSC.exe

## See also

- [PowerToys installation guide][03]
- [WinGet Configuration documentation][04]
- [Microsoft DSC documentation][05]

<!-- Link reference definitions -->
[01]: psdsc.md
[02]: microsoft-dsc.md
[03]: ../install.md
[04]: /windows/package-manager/configuration/
[05]: /powershell/dsc/overview