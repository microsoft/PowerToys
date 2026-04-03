# Command Palette Extension – Copilot Instructions

Concise guidance for AI-assisted development of this Command Palette extension.

## Project Structure

| Folder | Purpose |
|--------|---------|
| `Pages/` | Extension pages (ListPage, ContentPage, DynamicListPage implementations) |
| `Assets/` | Icons and images (StoreLogo.png, etc.) |
| `Properties/` | Launch settings and publish profiles |
| Root `.cs` files | Extension entry point, COM server (Program.cs), and CommandsProvider |

## Key Conventions

- Extensions run **out-of-process** via COM server registration
- `Program.cs` hosts the COM server — do not modify the hosting pattern
- The `CommandProvider` subclass is the entry point for all commands
- Pages are **ICommand** implementations — they can be used anywhere commands are used
- Always **Deploy** (not just Build) to register the MSIX package
- After deploying, use the **Reload** command in Command Palette to refresh

## Build & Deploy

1. In Visual Studio, use **Build > Deploy** (not just Build)
2. In Command Palette, run `Reload` → select "Reload Command Palette extensions"
3. For debugging, run in Debug configuration (F5) and check Output window (Ctrl+Alt+O)

## Source Control

If using git, remove these lines from `.gitignore` (needed for deployment):
- `**/Properties/launchSettings.json`
- `*.pubxml`

## Available Skills

This project includes Copilot skills for common workflows:
- **add-adaptive-card-form** — Create form-based UI with Adaptive Cards
- **add-extension-settings** — Add a settings page to your extension
- **add-dock-band** — Add persistent toolbar widgets
- **add-fallback-commands** — Add catch-all search commands
- **publish-extension** — Publish to Microsoft Store or WinGet

## Documentation

- [Creating an extension](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension)
- [Extension samples](https://learn.microsoft.com/windows/powertoys/command-palette/samples)
- [Extensibility overview](https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview)
