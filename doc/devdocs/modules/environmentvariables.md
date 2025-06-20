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
- When a profile variable has the same name as an existing User variable, a backup is created with a naming pattern: `VARIABLE_NAME_powertoys_PROFILE_NAME`
