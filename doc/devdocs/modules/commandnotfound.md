# Command Not Found

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/cmd-not-found)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-CommandNotFound)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-CommandNotFound)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-CommandNotFound)

## Overview
Command Not Found is a PowerToys module that suggests package installations when you attempt to run a command that isn't available on your system. It integrates with the Windows command line to provide helpful suggestions for installing missing commands through package managers.

## How it Works
When you attempt to execute a command in the terminal that isn't found, the Command Not Found module intercepts this error and checks if the command is available in known package repositories. If a match is found, it suggests the appropriate installation command.

## Installation
The Command Not Found module requires the Microsoft.WinGet.CommandNotFound PowerShell module to function properly. When enabling the module through PowerToys, it automatically attempts to install this dependency.

The installation is handled by the following script:
```powershell
# Located in PowerToys\src\settings-ui\Settings.UI\Assets\Settings\Scripts\EnableModule.ps1
Install-Module -Name Microsoft.WinGet.CommandNotFound -Force
```

## Usage
1. Enable the Command Not Found module in PowerToys settings.
2. Open a terminal and try to run a command that isn't installed on your system.
3. If the command is available in a package, you'll see a suggestion for how to install it.

Example:
```
C:\> kubectl
'kubectl' is not recognized as an internal or external command, operable program, or batch file.

Command 'kubectl' not found, but can be installed with:
    winget install -e --id Kubernetes.kubectl
```

## Technical Details
The Command Not Found module leverages the Microsoft.WinGet.CommandNotFound PowerShell module, which is maintained in a separate repository: https://github.com/microsoft/winget-command-not-found

The module works by registering a command-not-found handler that intercepts command execution failures and provides installation suggestions based on available packages in the WinGet repository.