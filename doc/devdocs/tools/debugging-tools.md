# PowerToys Debugging Tools

PowerToys includes several specialized tools to help with debugging and troubleshooting. These tools are designed to make it easier to diagnose issues with PowerToys features.

## FancyZones Debugging Tools

### FancyZones Hit Test Tool

- Location: `/tools/FancyZonesHitTest/`
- Purpose: Tests FancyZones layout selection logic
- Functionality:
  - Simulates mouse cursor positions
  - Highlights which zone would be selected
  - Helps debug zone detection issues

### FancyZones Draw Layout Test

- Location: `/tools/FancyZonesDrawLayoutTest/`
- Purpose: Tests FancyZones layout drawing logic
- Functionality:
  - Visualizes how layouts are drawn
  - Helps debug rendering issues
  - Tests different monitor configurations

### FancyZones Zonable Tester

- Location: `/tools/FancyZonesZonableTester/`
- Purpose: Tests if a window is "zonable" (can be moved to zones)
- Functionality:
  - Checks if windows match criteria for zone placement
  - Helps debug why certain windows can't be zoned

## Monitor Information Tools

### Monitor Info Report

- Location: `/tools/MonitorPickerTool/`
- Purpose: Diagnostic tool for identifying WinAPI bugs related to physical monitor detection
- Functionality:
  - Lists all connected monitors
  - Shows detailed monitor information
  - Helps debug multi-monitor scenarios

## Window Information Tools

### Styles Report Tool

- Location: `/tools/StylesReportTool/`
- Purpose: Collect information about an open window
- Functionality:
  - Reports window styles
  - Shows window class information
  - Helps debug window-related issues in modules like FancyZones

### Build Process

The Styles Report Tool is built separately from the main PowerToys solution:

```
nuget restore .\tools\StylesReportTool\StylesReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\StylesReportTool\StylesReportTool.sln
```

## Shell-Related Debugging Tools

### PowerRenameContextMenu Test

- Location: `/tools/PowerRenameContextMenuTest/`
- Purpose: Tests PowerRename context menu integration
- Functionality:
  - Simulates right-click context menu
  - Helps debug shell extension issues

## Verification Tools

### Verification Scripts

- Location: `/tools/verification-scripts/`
- Purpose: Scripts to verify PowerToys installation and functionality
- Functionality:
  - Verify binary integrity
  - Check registry entries
  - Test module loading

## Other Debugging Tools

### Clean Up Tool

- Location: `/tools/CleanUp/`
- Purpose: Clean up PowerToys installation artifacts
- Functionality:
  - Removes registry entries
  - Deletes settings files
  - Helps with clean reinstallation

### Using Debugging Tools

1. Most tools can be run directly from the command line
2. Some tools require administrator privileges
3. Tools are typically used during development or for advanced troubleshooting
4. Bug Report Tool can collect and package the output from several of these tools

## Adding New Debugging Tools

When creating new debugging tools:

1. Place the tool in the `/tools/` directory
2. Follow existing naming conventions
3. Document the tool in this file
4. Include a README.md in the tool's directory
5. Consider adding the tool's output to the Bug Report Tool if appropriate
