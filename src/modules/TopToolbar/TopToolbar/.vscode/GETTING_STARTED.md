# Getting Started with VSCode Debug for TopToolbar

## Step-by-Step Setup

### Step 1: Open TopToolbar in VSCode

```bash
# Navigate to TopToolbar folder
cd c:\PowerToys\src\modules\TopToolbar\TopToolbar

# Open in VSCode
code .
```

### Step 2: Install Recommended Extensions

When VSCode opens, you'll see a notification about recommended extensions:

1. Click "Show Recommendations" or go to Extensions (`Ctrl+Shift+X`)
2. Install the following:
   - **C# Dev Kit** by Microsoft (ms-dotnettools.csharp)
   - **.NET Runtime Install Tool** (ms-dotnettools.vscode-dotnet-runtime)
   - *Optional:* GitLens for version control (eamodio.gitlens)

3. Reload VSCode window when prompted

### Step 3: Verify .NET SDK Installation

```powershell
# Check installed .NET versions
dotnet --list-sdks

# Should show .NET 8.0 or later
# Example output:
# 8.0.x [C:\Program Files\dotnet\sdk\8.0.x]
```

If .NET SDK is not installed:
1. Visit https://dotnet.microsoft.com/download
2. Download .NET 8.0 SDK or later
3. Run installer and follow instructions
4. Restart terminal and verify installation

### Step 4: Build TopToolbar

**Option A: Using VSCode UI**
1. Press `Ctrl+Shift+B` (default build task)
2. Select `build-debug-x64`
3. Wait for compilation to complete

**Option B: Using Terminal**
```powershell
# In VSCode Terminal (Ctrl+`)
dotnet build -c Debug -p:Platform=x64 TopToolbar.csproj
```

### Step 5: Start Debugging

1. Press `F5` or go to Debug view (`Ctrl+Shift+D`)
2. Select debug configuration:
   - **"Launch TopToolbar (x64 Debug)"** (recommended for first time)
   - Press `F5` or click the green play button
3. App should launch with debugger attached

### Step 6: Set a Breakpoint

1. Open a source file (e.g., `TopToolbarWindow.xaml.cs`)
2. Click in the gutter (left margin of line numbers)
3. Red dot indicates breakpoint is set
4. Interact with the application to trigger breakpoint
5. Execution pauses at breakpoint
6. Inspect variables in Debug panel

## Common Debug Tasks

### Debug Single File
```powershell
# Build only
dotnet build -c Debug TopToolbar.csproj

# Or via UI: Ctrl+Shift+B
```

### Debug Specific Architecture
```powershell
# ARM64 debug
dotnet build -c Debug -p:Platform=ARM64 TopToolbar.csproj

# x64 debug
dotnet build -c Debug -p:Platform=x64 TopToolbar.csproj
```

### Attach to Running Process
1. Start TopToolbar normally
2. Press `F5` in VSCode
3. Select "Attach to TopToolbar Process"
4. VSCode displays list of running processes
5. Select TopToolbar process
6. Debugger attaches

### Clean Build
```powershell
# Clear build artifacts
dotnet clean TopToolbar.csproj

# Or via UI: Ctrl+Shift+P → "Tasks: Run Task" → "clean"
```

## Debugging Features

### Breakpoints
- **Toggle**: Click gutter or press `F9`
- **View**: Debug view → Breakpoints panel
- **Conditional**: Right-click breakpoint → Add breakpoint condition

### Watch Variables
1. Debug view → Watch panel
2. Click "+" to add expression
3. Type variable name or expression
4. Watch updates as execution continues

### Call Stack
- Debug view → Call Stack panel
- Shows execution flow
- Click frame to navigate to code

### Debug Console
1. Press `Ctrl+Shift+Y`
2. Evaluate C# expressions
3. View debug output
4. Exception details displayed here

### Step Operations
- **F10**: Step Over (next line)
- **F11**: Step Into (enter function)
- **Shift+F11**: Step Out (exit function)
- **F5**: Continue execution

## Keyboard Shortcuts Reference

| Shortcut | Action |
|----------|--------|
| `F5` | Start/Continue Debug |
| `Shift+F5` | Stop Debug |
| `Ctrl+Shift+D` | Open Debug View |
| `F9` | Toggle Breakpoint |
| `F10` | Step Over |
| `F11` | Step Into |
| `Shift+F11` | Step Out |
| `Ctrl+Shift+B` | Run Build Task |
| `Ctrl+Shift+P` | Command Palette |
| `Ctrl+\`` | Toggle Terminal |
| `Ctrl+Shift+Y` | Debug Console |

## Troubleshooting

### Issue: "Program not found"
**Solution:**
1. Verify build succeeded (check `bin/x64/Debug/` folder)
2. Run clean build: `dotnet clean && dotnet build -c Debug`
3. Check file path in launch.json

### Issue: "Cannot attach to process"
**Solution:**
1. Ensure application is running
2. May need Administrator privileges
3. Verify architecture matches (x64 vs ARM64)

### Issue: "Breakpoint not hit"
**Solution:**
1. Ensure Debug build (not Release)
2. Check symbols are loaded
3. Verify code path is executed

### Issue: Extension not loading
**Solution:**
1. Go to Extensions (`Ctrl+Shift+X`)
2. Search for "C# Dev Kit"
3. Click Install
4. Reload VSCode (`Ctrl+Shift+P` → "Reload Window")

### Issue: Build fails with errors
**Solution:**
1. Check error output in Terminal
2. Verify .NET SDK version: `dotnet --version`
3. Clean build: `dotnet clean`
4. Check TopToolbar.csproj syntax

## Project Structure

```
TopToolbar/
├── .vscode/                    # VSCode configuration
│   ├── launch.json            # Debug configurations
│   ├── tasks.json             # Build tasks
│   ├── settings.json          # Editor settings
│   ├── extensions.json        # Recommended extensions
│   ├── DEBUG_SETUP.md         # Detailed setup guide
│   └── VSCODE_CONFIG_SUMMARY.md # Configuration summary
├── TopToolbar.csproj          # Project file
├── TopToolbarXAML/            # XAML UI code
│   └── ToolbarWindow.xaml.cs
├── Services/                  # Services layer
├── Models/                    # Data models
├── Providers/                 # Provider implementations
└── bin/                       # Build output
    ├── x64/Debug/            # x64 debug build
    └── ARM64/Debug/          # ARM64 debug build
```

## Next Steps

1. ✅ Set up VSCode with debug configurations
2. ✅ Build TopToolbar successfully
3. ✅ Set breakpoints and debug code
4. 📖 Read DEBUG_SETUP.md for advanced topics
5. 🚀 Start developing with VSCode

## Additional Resources

- [VSCode Debugging Guide](https://code.visualstudio.com/docs/editor/debugging)
- [C# Development in VSCode](https://code.visualstudio.com/docs/csharp/intro-to-cs)
- [.NET Development Guide](https://learn.microsoft.com/en-us/dotnet/)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/)

## Support

For issues or questions:
1. Check DEBUG_SETUP.md troubleshooting section
2. Review VSCode documentation
3. Check TopToolbar project README
4. Consult .NET and C# documentation
