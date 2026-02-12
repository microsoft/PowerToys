# PowerToys Developer Documentation

Welcome to the PowerToys developer documentation. This documentation provides information for developers who want to contribute to PowerToys or understand how it works.

## Getting Started

### Prerequisites

1. Windows 10 April 2018 Update (version 1803) or newer
1. [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) (recommended) or Visual Studio 2022 17.4+ with the following workloads/components:
   - Desktop Development with C++
   - WinUI application development
   - .NET desktop development
   - Windows 10 SDK (10.0.22621.0)
   - Windows 11 SDK (10.0.26100.3916)
1. .NET 8 SDK
1. Enable long paths in Windows (see [Enable Long Paths](https://docs.microsoft.com/windows/win32/fileio/maximum-file-path-limitation#enabling-long-paths-in-windows-10-version-1607-and-later) for details)

> **Tip:** You can install Visual Studio with all required workloads automatically using the [WinGet configuration files](https://github.com/microsoft/PowerToys/tree/main/.config) in the repository:
> ```powershell
> winget configure .config\configuration.winget
> ```
> Pick the file that matches your VS edition (e.g., `configuration.vsProfessional.winget` or `configuration.vsEnterprise.winget`).

### Fork, Clone, and Set Up

1. Fork the repo on GitHub if you haven't already
1. Clone your fork locally
1. Run the automated setup script (**recommended**):

```powershell
.\tools\build\setup-dev-environment.ps1
```

This script will:
- Enable Windows long path support (requires administrator privileges)
- Enable Windows Developer Mode (requires administrator privileges)
- Guide you through installing required Visual Studio components from `.vsconfig`
- Initialize git submodules

Run with `-Help` to see all available options.

<details>
<summary><strong>Manual setup (if you prefer not to use the script)</strong></summary>

#### Install Visual Studio dependencies

1. Open the `PowerToys.slnx` file.
1. If you see a dialog that says `install extra components` in the solution explorer pane, click `install`

Alternatively, import the `.vsconfig` file from the repository root using Visual Studio Installer to install all required workloads.

#### Initialize submodules

This is a one-time step required before you can compile most parts of PowerToys.

1. Open a terminal
1. Navigate to the folder you cloned PowerToys to.
1. Run `git submodule update --init --recursive`

</details>

### Building

#### Using Visual Studio

- Open `PowerToys.slnx` in Visual Studio.
- In the `Solutions Configuration` drop-down menu select `Release` or `Debug`.
- From the `Build` menu choose `Build Solution`, or press <kbd>Control</kbd>+<kbd>Shift</kbd>+<kbd>b</kbd> on your keyboard.
- The build process may take several minutes depending on your computer's performance. Once it completes, the PowerToys binaries will be in your repo under `x64\Release\`.
    - You can run `x64\Release\PowerToys.exe` directly without installing PowerToys, but some modules (i.e. PowerRename, ImageResizer, File Explorer extension etc.) will not be available unless you also build the installer and install PowerToys.

#### Using Command Line

You can also build from the command line using the provided scripts in `tools\build\`:

```powershell
# Build the full solution (auto-detects platform)
.\tools\build\build.ps1

# Build with specific configuration
.\tools\build\build.ps1 -Platform x64 -Configuration Release

# Build only essential projects (runner + settings) for faster iteration
.\tools\build\build-essentials.ps1

# Build everything including the installer (Release only)
.\tools\build\build-installer.ps1
```

### Debugging

See [Debugging](development/debugging.md) for detailed debugging techniques, including Visual Studio setup, attaching to child processes, and troubleshooting build errors.

### Creating a New PowerToy

See [Creating a New PowerToy](development/new-powertoy.md) for an end-to-end guide covering module architecture, settings integration, installer packaging, and testing.

## Development Guidelines

- [Coding Guidelines](development/guidelines.md) - Development guidelines and best practices
- [Coding Style](development/style.md) - Code formatting and style conventions
- [Logging and Telemetry](development/logging.md) - How to use logging and telemetry
- [Localization](development/localization.md) - How to support multiple languages
- [UI Testing](development/ui-tests.md) - How to write UI tests for PowerToys
- [Developing with VS Code](development/dev-with-vscode.md) - Build, debug, and contribute using VS Code

## Rules

- **Follow the pattern of what you already see in the code.**
- [Coding style](development/style.md).
- Try to package new functionality/components into libraries that have nicely defined interfaces.
- Package new functionality into classes or refactor existing functionality into a class as you extend the code.
- When adding new classes/methods/changing existing code, add new unit tests or update the existing tests.

## GitHub Workflow

- Before starting to work on a fix/feature, make sure there is an open issue to track the work.
- Add the `In progress` label to the issue, if not already present. Also add a `Cost-Small/Medium/Large` estimate and make sure all appropriate labels are set.
- If you are a community contributor, you will not be able to add labels to the issue; in that case just add a comment saying that you have started work on the issue and try to give an estimate for the delivery date.
- If the work item has a medium/large cost, using the markdown task list, list each sub item and update the list with a check mark after completing each sub item.
- **Before opening a PR, ensure your changes build successfully locally and functionality tests pass.** This is especially important for AI-assisted (vibe coding) contributions—always verify AI-generated code works as intended. Exploratory PRs or draft PRs for discussion are exceptions.
- When opening a PR, follow the PR template.
- When you'd like the team to take a look (even if the work is not yet fully complete) mark the PR as 'Ready For Review' so that the team can review your work and provide comments, suggestions, and request changes. It may take several cycles, but the end result will be solid, testable, conformant code that is safe for us to merge.
- When the PR is approved, let the owner of the PR merge it. For community contributions, the reviewer who approved the PR can also merge it.
- Use the `Squash and merge` option to merge a PR. If you don't want to squash it because there are logically different commits, use `Rebase and merge`.
- Close issues automatically when referenced in a PR. You can use [closing keywords](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue#linking-a-pull-request-to-an-issue-using-a-keyword) in the body of the PR to have GitHub automatically link your PR to the issue.

## Core Architecture

- [Architecture Overview](core/architecture.md) - Overview of the PowerToys architecture and module interface
- [Runner and System tray](core/runner.md) - Details about the PowerToys Runner process
- [Settings](core/settings/readme.md) - Documentation on the settings system
- [Installer](core/installer.md) - Information about the PowerToys installer
- [Modules](modules/readme.md) - Documentation for individual PowerToys modules

## Common Components

- [Context Menu Handlers](common/context-menus.md) - How PowerToys implements and registers Explorer context menu handlers
- [Monaco Editor](common/monaco-editor.md) - How PowerToys uses the Monaco code editor component across modules

## Tools

- [Tools Overview](tools/readme.md) - Overview of tools in PowerToys
- [Build Tools](tools/build-tools.md) - Tools that help building PowerToys
- [Bug Report Tool](tools/bug-report-tool.md) - Tool for collecting logs and system information
- [Debugging Tools](tools/debugging-tools.md) - Specialized tools for debugging
- [Fuzzing Testing](tools/fuzzingtesting.md) - How to implement and run fuzz testing for PowerToys modules

## Processes

- [Release Process](processes/release-process.md) - How PowerToys releases are prepared and published
- [Update Process](processes/update-process.md) - How PowerToys updates work
- [GPO Implementation](processes/gpo.md) - Group Policy Objects implementation details

## Other Resources

- [aka.ms links](akaLinks.md) - List of short links
- [Issue/PR commands](commands.md) - Special commands for managing issues and pull requests

## Building the Installer

Our installer is two parts, an EXE and an MSI.  The EXE (Bootstrapper) contains the MSI and handles more complex installation logic. 
- The EXE installs all prerequisites and installs PowerToys via the MSI. It has additional features such as the installation flags (see below).
- The MSI installs the PowerToys binaries.

The installer can only be compiled in `Release` mode; steps 1 and 2 must be performed before the MSI can be compiled.

1. Compile `PowerToys.slnx`. Instructions are listed above.
1. Compile `BugReportTool.sln` tool. Path from root: `tools\BugReportTool\BugReportTool.sln` (details listed below)
1. Compile `StylesReportTool.sln` tool. Path from root: `tools\StylesReportTool\StylesReportTool.sln` (details listed below)
1. Compile `PowerToysSetup.slnx` Path from root: `installer\PowerToysSetup.slnx` (details listed below)

See [Installer](core/installer.md) for more details on building and debugging the installer.
