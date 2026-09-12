# Mouse Without Borders module

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/mouse-without-borders)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Mouse%20Without%20Borders%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Mouse%20Without%20Borders%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Mouse+Without+Borders%22)

This file contains the documentation for the Mouse Without Borders PowerToy module.
## Table of Contents:
- [Mouse Without Borders module](#mouse-without-borders-module)
  - [Table of Contents](#table-of-contents)
  - [Status colors](#status-colors)
  - [Experimental non-console sessions (Debug only)](#experimental-non-console-sessions-debug-only)

## Status colors
The following colors are used to indicate the connection status to the user when trying to connect to another computer:

| Connection Status | Color    | Hex Code    |
| :-----: | :---: | :---: |
| NA | Dark Grey   | `#00717171`  |
| Resolving | Yellow   | `#FFFFFF00`   |
| Connecting | Orange   | `#FFFFA500`   |
| Handshaking | Blue   | `#FF0000FF`   |
| Error | Red  | `#FFFF0000`   |
| ForceClosed | Purple   | `#FF800080`   |
| InvalidKey | Brown   | `#FFA52A2A`   |
| Timeout | Pink   | `#FFFFC0CB`   |
| SendError | Maroon   | `#FF800000`   |
| Connected | Green   | `#FF008000`   |

## Experimental non-console sessions (Debug only)

MWB normally requires its process session to match `WTSGetActiveConsoleSessionId()`.
An active RDP desktop is not necessarily the console, and Windows Sandbox can
report no attached console session. For local debugging, a **Debug build** can opt
in to non-console sessions with the process environment variable
`POWERTOYS_MWB_ALLOW_NONCONSOLE=1`.

The variable is read once when MWB starts. Set it only in the environment of the
experimental Runner process, so its MWB child inherits it, and in the Sandbox
endpoint's launch environment. It is not a persisted settings.json property or a
Settings UI option. Unset, empty, and other values keep the normal console-only
behavior. **Release builds ignore the variable.**

The override is limited to user processes on the `default` desktop; service mode,
LocalSystem, logon, and screensaver desktops retain the existing checks. Desktop
activity checks are also retained: this does not enable input while RDP is
disconnected or Windows is locked. IPC peer verification, encryption, and firewall
requirements are unchanged. An enabled override is logged once at startup.

This flag permits an experiment; it does not establish support for RDP or Sandbox.
Keep Sandbox clipboard redirection disabled, keep its viewer out of focus while
testing remote input, and use disabled/disconnected MWB negative controls to avoid
mistaking Sandbox's own redirection for MWB behavior. Do not use this experiment to
sign off service or secure-desktop behavior.

The deferred host/Sandbox preparation scripts are in
`src\modules\MouseWithoutBorders\Tests\SandboxExperiment`. Preparation must not
launch either endpoint; start the experiment separately on an active, unlocked
desktop. A CI experiment using this flag needs the same Debug product payload,
not normal Release binaries.
