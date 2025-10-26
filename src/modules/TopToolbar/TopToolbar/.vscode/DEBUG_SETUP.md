# VSCode Debug Setup for TopToolbar

This directory contains VSCode debug configuration for the TopToolbar project.

## Quick Start

### Prerequisites

1. **Install Required Extensions** (VSCode will prompt you)
   - C# Dev Kit (ms-dotnettools.csharp)
   - .NET Runtime Install Tool (ms-dotnettools.vscode-dotnet-runtime)

2. **Install .NET SDK**
   - .NET 8.0 or later
   - Download from: https://dotnet.microsoft.com/download

3. **Required Tools**
   - Visual Studio 2022 (Insiders or higher for WinUI 3 support)
   - Windows SDK for XAML tools

### Setup Steps

1. **Open the TopToolbar folder in VSCode**
   ```bash
   code c:\PowerToys\src\modules\TopToolbar\TopToolbar
   ```

2. **Install recommended extensions** when prompted

3. **Configure launch settings**
   - Press `Ctrl+Shift+D` to open Debug view
   - Select configuration from the dropdown:
     - "Launch TopToolbar (x64 Debug)" - for x64 architecture
     - "Launch TopToolbar (ARM64 Debug)" - for ARM64 architecture
     - "Attach to TopToolbar Process" - to attach to running process

## Debug Configurations

### Launch TopToolbar (x64 Debug)
- Builds TopToolbar in Debug mode for x64 architecture
- Runs the executable from `bin/x64/Debug/TopToolbar.exe`
- Places breakpoints and inspects variables
- Recommended for initial debugging

### Attach to TopToolbar Process
- Attaches debugger to running TopToolbar process
- Useful for debugging already-running applications
- VSCode shows list of processes to select from

### Launch TopToolbar (ARM64 Debug)
- Builds TopToolbar in Debug mode for ARM64 architecture
- Runs the executable from `bin/ARM64/Debug/TopToolbar.exe`
- For ARM-based Windows devices

## Build Tasks

Available build tasks (press `Ctrl+Shift+B` to run):

### Default Build Task
- **build-debug-x64**: Debug build for x64 platform

### Other Tasks
- **build-debug-arm64**: Debug build for ARM64 platform
- **build-release-x64**: Release build for x64 platform
- **clean**: Clean build artifacts
- **run-debug-x64**: Run debug build for x64

### Running a Task
1. Press `Ctrl+Shift+B` (default build) or
2. Press `Ctrl+Shift+P` and select "Tasks: Run Task"
3. Choose from available tasks

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `F5` | Start Debugging (Launch) |
| `Ctrl+Shift+D` | Open Debug View |
| `Ctrl+Shift+B` | Run Default Build Task |
| `Ctrl+Shift+P` | Command Palette (run other tasks) |
| `F9` | Toggle Breakpoint |
| `F10` | Step Over |
| `F11` | Step Into |
| `Shift+F11` | Step Out |
| `Ctrl+K Ctrl+I` | Show Hover (Debug) |

## Debugging Workflow

### Setting Breakpoints
1. Click in the gutter (left margin) of code editor
2. Red circle appears indicating breakpoint
3. Launch debugger with F5
4. Execution stops at breakpoint

### Inspecting Variables
- Hover over variables to see current values
- Use Debug Console to evaluate expressions
- Watch panel for complex expressions

### Debug Console
- Press `Ctrl+Shift+Y` or click Debug Console tab
- Execute C# expressions
- View debug output and logs

## Troubleshooting

### "Program not found" error
- Ensure you've built the project successfully
- Check if binary exists in `bin/x64/Debug/` or `bin/ARM64/Debug/`
- Run build task before debugging

### "Cannot attach to process" error
- Ensure TopToolbar process is running
- Check permissions (may need Administrator rights)
- Verify process architecture matches (x64 vs ARM64)

### Breakpoints not hit
- Ensure code is built in Debug mode (not Release)
- Check symbols are loaded (Debug Console shows debug info)
- Verify breakpoint is in actual code path

### Extension not found error
- Install C# Dev Kit from Extensions marketplace
- Reload VSCode window
- Verify installation in Extensions view

## Configuration Files

### launch.json
Defines debug launch configurations:
- Program paths for different architectures
- Pre-launch tasks
- Debug console behavior

### tasks.json
Defines build and run tasks:
- Build commands for different configurations
- Clean task
- Run tasks

### settings.json
VSCode settings specific to this workspace:
- C# analyzer settings
- Code formatting options
- OmniSharp configuration

### extensions.json
Recommended extensions for this project:
- C# extension
- .NET runtime tools
- XAML tools
- Git integration

## Additional Resources

- [VSCode Debugging Guide](https://code.visualstudio.com/docs/editor/debugging)
- [C# in VSCode](https://code.visualstudio.com/docs/languages/csharp)
- [Debug C# Applications](https://code.visualstudio.com/docs/csharp/debugging)
- [TopToolbar Documentation](../README.md)

## Notes

- Ensure Windows Defender or antivirus excludes VSCode and dotnet tools
- For WinUI 3 development, Visual Studio 2022 Insiders recommended
- Debug builds are larger; use Release builds for distribution
- Some features may require Administrator privileges
