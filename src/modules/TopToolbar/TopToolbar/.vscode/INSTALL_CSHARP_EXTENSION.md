# Installing C# Extension for VSCode Debugging

## What You Need

The error "Configured debug type 'coreclr' is not supported" means the C# debugger extension is not installed.

## Installation Options

### Option 1: Quick Install (Recommended)

Click the button in VSCode error dialog:
1. Click "Install coreclr Extension" button
2. Wait for installation to complete
3. VSCode reloads automatically
4. Press F5 again to start debugging

### Option 2: Install via Extensions Menu

1. Press `Ctrl+Shift+X` to open Extensions
2. Search for: `ms-dotnettools.csharp`
3. Click "Install" button
4. Wait for installation (takes 1-2 minutes)
5. Click "Reload Window" when prompted

### Option 3: Install via Command Palette

1. Press `Ctrl+Shift+P`
2. Type: `Extensions: Install Extensions`
3. Press Enter
4. Search for: `C# Dev Kit`
5. Click Install

## What Gets Installed

Installing C# extension provides:
- ✓ coreclr debugger (for .NET debugging)
- ✓ Code completion and IntelliSense
- ✓ Code formatting
- ✓ Syntax highlighting
- ✓ Error detection
- ✓ Test explorer
- ✓ XAML support

## After Installation

1. **Reload Window** (if not auto-reloaded)
   - Press `Ctrl+Shift+P`
   - Type: `Developer: Reload Window`
   - Press Enter

2. **Start Debugging Again**
   - Press `F5`
   - Select debug configuration
   - Application should launch with debugger

3. **Verify Installation**
   - Check Extensions view
   - Search for "C#"
   - Should show "C# Dev Kit" with checkmark

## Full Extension Recommendations

Install these for complete development experience:

| Extension | Command |
|-----------|---------|
| C# Dev Kit | `ms-dotnettools.csharp` |
| .NET Runtime | `ms-dotnettools.vscode-dotnet-runtime` |
| XAML Tools | `ms-dotnettools.vscode-xaml-tools` |
| GitHub Copilot | `GitHub.copilot` |
| GitLens | `eamodio.gitlens` |

## Troubleshooting Installation

### Slow Installation
- Normal for first install (large download)
- Can take 2-5 minutes
- Don't close VSCode during installation

### Installation Failed
- Check internet connection
- Restart VSCode
- Try manual installation:
  1. Go to https://marketplace.visualstudio.com
  2. Search "C# Dev Kit"
  3. Click "Install" button in browser
  4. Select "Open with Visual Studio Code"

### Extension Not Working After Install
- Reload Window: `Ctrl+Shift+P` → "Reload Window"
- Restart VSCode completely
- Check extension is enabled in Extensions view

## After Successful Installation

### Now You Can:
- ✓ Launch debug sessions with F5
- ✓ Set breakpoints with F9
- ✓ Inspect variables
- ✓ Step through code
- ✓ Evaluate expressions in debug console

### Next Steps:
1. Press F5 to start debugging
2. Set breakpoint by clicking gutter
3. Application launches with debugger
4. Breakpoint pauses execution
5. Inspect variables in Debug panel

## Getting Help

If installation still doesn't work:

1. **Check VSCode Version**
   - Should be latest stable or insiders
   - Update from Help menu if needed

2. **Check .NET SDK**
   ```powershell
   dotnet --version
   # Should show 8.0 or later
   ```

3. **Check Firewall**
   - VSCode may need internet access for extensions
   - Add VSCode to firewall exceptions if needed

4. **Manual Marketplace Link**
   - Direct link: https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp

## Quick Reference

| Action | Steps |
|--------|-------|
| Install C# | Ctrl+Shift+X → Search "C#" → Install |
| Reload | Ctrl+Shift+P → "Reload Window" |
| Debug | F5 → Select config |
| Breakpoint | Click line gutter |
| Step | F10 (over) or F11 (into) |

---

**Status**: After C# extension installation, coreclr debugging will be available.

**Time to Install**: 2-5 minutes

**Next**: Press F5 to start debugging TopToolbar
