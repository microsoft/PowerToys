# PowerToys Developer Documentation

Welcome to the PowerToys developer documentation. This documentation provides information for developers who want to contribute to PowerToys or understand how it works.

## Core Architecture

- [Architecture Overview](core/architecture.md) - Overview of the PowerToys architecture and module interface
- [Runner and System tray](core/runner.md) - Details about the PowerToys Runner process
- [Settings](core/settings/readme.md) - Documentation on the settings system
- [Installer](core/installer.md) - Information about the PowerToys installer
- [Modules](modules/readme.md) - Documentation for individual PowerToys modules

## Common Components

- [Context Menu Handlers](common/context-menus.md) - How PowerToys implements and registers Explorer context menu handlers
- [Monaco Editor](common/monaco-editor.md) - How PowerToys uses the Monaco code editor component across modules
- [Logging and Telemetry](development/logging.md) - How to use logging and telemetry
- [Localization](development/localization.md) - How to support multiple languages

## Development Guidelines

- [Coding Guidelines](development/guidelines.md) - Development guidelines and best practices
- [Coding Style](development/style.md) - Code formatting and style conventions
- [UI Testing](development/ui-tests.md) - How to write UI tests for PowerToys
- [Debugging](development/debugging.md) - Techniques for debugging PowerToys

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

## Fork, Clone, Branch and Create your PR

Once you've discussed your proposed feature/fix/etc. with a team member, and an approach or a spec has been written and approved, it's time to start development:

1. Fork the repo on GitHub if you haven't already
1. Clone your fork locally
1. Create a feature branch
1. Work on your changes
1. Create a [Draft Pull Request (PR)](https://github.blog/2019-02-14-introducing-draft-pull-requests/)
1. When ready, mark your PR as "ready for review".

## Rules

- **Follow the pattern of what you already see in the code.**
- [Coding style](style.md).
- Try to package new functionality/components into libraries that have nicely defined interfaces.
- Package new functionality into classes or refactor existing functionality into a class as you extend the code.
- When adding new classes/methods/changing existing code, add new unit tests or update the existing tests.

## GitHub Workflow

- Before starting to work on a fix/feature, make sure there is an open issue to track the work.
- Add the `In progress` label to the issue, if not already present. Also add a `Cost-Small/Medium/Large` estimate and make sure all appropriate labels are set.
- If you are a community contributor, you will not be able to add labels to the issue; in that case just add a comment saying that you have started work on the issue and try to give an estimate for the delivery date.
- If the work item has a medium/large cost, using the markdown task list, list each sub item and update the list with a check mark after completing each sub item.
- When opening a PR, follow the PR template.
- When you'd like the team to take a look (even if the work is not yet fully complete) mark the PR as 'Ready For Review' so that the team can review your work and provide comments, suggestions, and request changes. It may take several cycles, but the end result will be solid, testable, conformant code that is safe for us to merge.
- When the PR is approved, let the owner of the PR merge it. For community contributions, the reviewer who approved the PR can also merge it.
- Use the `Squash and merge` option to merge a PR. If you don't want to squash it because there are logically different commits, use `Rebase and merge`.
- Close issues automatically when referenced in a PR. You can use [closing keywords](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue#linking-a-pull-request-to-an-issue-using-a-keyword) in the body of the PR to have GitHub automatically link your PR to the issue.

## Compiling PowerToys

### Prerequisites for Compiling PowerToys

1. Windows 10 April 2018 Update (version 1803) or newer
1. Visual Studio Community/Professional/Enterprise 2022 17.4 or newer
1. A local clone of the PowerToys repository
1. Enable long paths in Windows (see [Enable Long Paths](https://docs.microsoft.com/windows/win32/fileio/maximum-file-path-limitation#enabling-long-paths-in-windows-10-version-1607-and-later) for details)

### Install Visual Studio dependencies

1. Open the `PowerToys.slnx` file.
1. If you see a dialog that says `install extra components` in the solution explorer pane, click `install`

### Get Submodules to compile

We have submodules that need to be initialized before you can compile most parts of PowerToys.  This should be a one-time step.

1. Open a terminal
1. Navigate to the folder you cloned PowerToys to.
1. Run `git submodule update --init --recursive`

### Compiling Source Code

- Open `PowerToys.slnx` in Visual Studio.
- In the `Solutions Configuration` drop-down menu select `Release` or `Debug`.
- From the `Build` menu choose `Build Solution`, or press <kbd>Control</kbd>+<kbd>Shift</kbd>+<kbd>b</kbd> on your keyboard.
- The build process may take several minutes depending on your computer's performance. Once it completes, the PowerToys binaries will be in your repo under `x64\Release\`.
    - You can run `x64\Release\PowerToys.exe` directly without installing PowerToys, but some modules (i.e. PowerRename, ImageResizer, File Explorer extension etc.) will not be available unless you also build the installer and install PowerToys.

## Compile the installer

Our installer is two parts, an EXE and an MSI.  The EXE (Bootstrapper) contains the MSI and handles more complex installation logic. 
- The EXE installs all prerequisites and installs PowerToys via the MSI. It has additional features such as the installation flags (see below).
- The MSI installs the PowerToys binaries.

The installer can only be compiled in `Release` mode; steps 1 and 2 must be performed before the MSI can be compiled.

1. Compile `PowerToys.slnx`. Instructions are listed above.
1. Compile `BugReportTool.sln` tool. Path from root: `tools\BugReportTool\BugReportTool.sln` (details listed below)
1. Compile `StylesReportTool.sln` tool. Path from root: `tools\StylesReportTool\StylesReportTool.sln` (details listed below)
1. Compile `PowerToysSetup.slnx` Path from root: `installer\PowerToysSetup.slnx` (details listed below)

See [Installer](core/installer.md) for more details on building and debugging the installer.

## How to create new PowerToys

See the instructions on [how to install the PowerToys Module project template](/tools/project_template). <br />
Specifications for the [PowerToys settings API](core/settings/readme.md).
