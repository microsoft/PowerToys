# Environment Variables

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/environment-variables)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Environment%20Variables%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Environment%20Variables%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Environment+Variables%22)
[Checklist](https://github.com/microsoft/PowerToys/blob/releaseChecklist/doc/releases/tests-checklist-template.md?plain=1#L744)

## Overview

Environment Variables is a PowerToys module that provides an easy and convenient way to manage Windows environment variables. It offers a modern user interface for viewing, editing, and managing both user and system environment variables.

## Features

- View and edit user and system environment variables in a unified interface
- Create profiles to group and manage sets of variables together
- Profile-based variable management with on/off toggles
- Automatic backup of existing variables when overridden by a profile
- Restoration of original values when profiles are disabled

## How It Works

### Profiles

Profiles are collections of environment variables that can be enabled or disabled together. When a profile is enabled:

1. Variables in the profile override existing User variables with the same name
2. Original values are automatically backed up for restoration when the profile is disabled
3. Only one profile can be active at a time

### Variable Precedence

The module follows this precedence order for environment variables:
1. Active profile variables (highest precedence)
2. User variables
3. System variables (lowest precedence)

## Architecture

The Environment Variables module is structured into three main components:

### Project Structure

```
EnvironmentVariables/               # Contains assets, main windows, and telemetry
EnvironmentVariablesModuleInterface # Interface definitions and package configurations
EnvironmentVariableUILib            # Abstracted UI methods and implementations
```

### Key Components

- **Main Window Framework**: Builds the modern Windows desktop UI, handles Windows messages, resource loading, and window closing operations
- **Project Configuration**: Defines settings and configurations for the module
- **UI Implementation**: Contains the user interface components and the backend logic

## Implementation Details

### Key Functions

- **OpenEnvironmentKeyIfExists**: Accesses environment information through registry keys
- **SetEnvironmentVariableFromRegistryWithoutNotify**: Sets variables directly to registry instead of using Environment API, avoiding the 1-second timeout for settings change notifications
- **GetVariables**: Reads variables directly from registry instead of using Environment API to prevent automatic variable expansion

### Technical Notes

- The module reads and writes variables directly to the registry instead of using the Environment API
- This direct registry access approach is used because the Environment API automatically expands variables and has a timeout for notifications
- When a profile variable has the same name as an existing User variable, a backup is created with the naming pattern: `VARIABLE_NAME_PowerToys_PROFILE_NAME`

## UI tests

**Do not run this suite on a working machine.** Use a disposable VM: the PATH scenario temporarily
replaces the current user's PATH with `path1;path2;path3`. Stopping the test host, a timeout, or a
machine reset can leave that PATH in place until recovery runs.

`src\modules\EnvironmentVariables\EnvironmentVariables.UITests` uses `UITestAutomation.Next`
and winappcli. Test variables and profiles have unique names; cleanup restores
the original unexpanded values, registry value kinds, and profile/settings files. TMP and System
PATH are not modified. Registry reads independently verify the persistent Windows environment
shown by the OS editor, while UIA assertions verify the module's Applied variables list.

Before modifying state, the fixture persists original variables and profile/module/global settings
in `%LOCALAPPDATA%\Microsoft\PowerToys\EnvironmentVariables\ui-tests-state.json`. A subsequent suite
invocation restores an interrupted run before taking a new snapshot. Cleanup attempts all restores
even if stopping the editor or one restore fails, reports failures, and retains the recovery journal
until restoration succeeds. Do not delete the journal to bypass recovery or attach it to a public
issue: it contains the original user state.

Coverage of [the UI-test checklist](https://github.com/microsoft/PowerToys/issues/40680):

| Checklist items | Automated scenario |
| --- | --- |
| 2 | Non-elevated launch, enabled User Add button, disabled System Add and edit controls |
| 3-5 (User) | Create, edit, and remove a User variable; verify registry and Applied variables |
| 6-10, 20 | Create an empty profile, edit it, create an enabled two-variable profile, switch/unapply profiles, and delete both from UI and `profiles.json` |
| 11-13 | Override a dedicated existing User variable, verify its backup, and restore it on unapply |
| 14-16 | Verify combined PATH and separate profile PATH entries; move, insert, and delete entries |
| 17-19 | Apply profile PATH, reopen the editor with the profile still enabled, then delete it and verify restoration |

Item 1 and the System-variable repetition of items 3-5 remain **manual-only**. The local VM
workflow requires a true standard-user test host; the current harness cannot approve credential
prompts on the UAC secure desktop or automate an elevated editor across that boundary. The suite
does not weaken UAC, store credentials, or report these scenarios as passing/skipped automated tests.

Build with `tools\build\build.cmd -Path src\modules\EnvironmentVariables\EnvironmentVariables.UITests -Platform x64 -Configuration Debug`.
Run the resulting `EnvironmentVariables.UITests.exe` with `--report-trx`; the category is
`EnvironmentVariables`. Use the `ui-tests-local-vm` workflow for full Windows 10 and Windows 11
default/constrained runs, then select `[EnvironmentVariables.UITests]` in UI Test Automation CI.

`TestState` intentionally owns this module's profile reconciliation and environment restoration.
Use `SettingsConfigHelper.PreserveFile` or `PreserveModuleSettings` for ordinary in-process file
snapshots; extracting a shared crash-recovery fixture is a separate framework change.
