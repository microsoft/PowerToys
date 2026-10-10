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
  - [Settings matrix reconciliation](#settings-matrix-reconciliation)
  - [Experimental non-console sessions](#experimental-non-console-sessions)
  - [Release, installed and physical-device validation](#release-installed-and-physical-device-validation)

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

## Settings matrix reconciliation

The Settings page reconciles `MachineMatrixString` with the current machine pool
when loading module settings. This operation must be idempotent: empty slots are
not removed machines, and only an actual removal or insertion should cause a
settings save. Rewriting an unchanged matrix retriggers the page's file watcher,
which can repeatedly reload and save settings on the UI dispatcher. Existing
device positions and the four-slot layout are preserved during reconciliation.

## Experimental non-console sessions

MWB normally requires its process session to match `WTSGetActiveConsoleSessionId()`.
An active RDP desktop is not necessarily the console, and Windows Sandbox can
report no attached console session. Both Debug and Release builds can explicitly
opt in using the hidden, **default-false** `AllowNonConsoleSessions` property in
`%LOCALAPPDATA%\Microsoft\PowerToys\MouseWithoutBorders\settings.json`:

```json
{
  "properties": {
    "AllowNonConsoleSessions": { "value": true }
  }
}
```

Merge this property into the existing file; do not replace the other settings.
MWB snapshots the setting at startup, so restart the module after changing it.
Missing or false retains the normal console-only restriction. Ordinary Settings
edits preserve the property, but there is no Settings UI control or command-line
configuration option. This is an experimental opt-in, not a supported RDP mode.

For compatibility, Debug builds also recognize the process-local environment
variable `POWERTOYS_MWB_ALLOW_NONCONSOLE=1`. **Release ignores environment-only
opt-in**; the autonomous suite uses the JSON property, not that variable. Do not
set a User/Machine environment variable to run an experiment.

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
desktop. The maintained autonomous suite uses one matching Release payload for
both endpoints and retains the production IPC/signing checks. Unsigned test
payloads require the existing disposable companion-signing mechanism and public
certificate trust in the dedicated VM and its guest, never an authentication
bypass. Installed/service behavior and physical two-PC sign-off remain separate.

## Release, installed and physical-device validation

The hidden setting depends on the **MWB binary version**, not the installer type.
A per-user or machine-wide installation containing this code reads the interactive
user's MWB JSON settings. A test can set the property in its disposable baseline,
then restore the original bytes. An older official binary does not gain this
behavior merely because the property is added to JSON.

These are separate validation axes:

| Coverage | What it proves | What it does not prove |
|---|---|---|
| Shared Release Sandbox suite | Real Settings pairing, authenticated same-version peers, owned TCP/input/clipboard behavior and recovery using one Release build | Installer registration, upgrade/uninstall, service/System or secure-desktop behavior |
| Installed per-user/machine checks | The actual installed paths, permissions, registration, firewall/setup and lifecycle behavior for that installer | Real two-device networking and hardware differences |
| Focused physical two-PC checks | Real LAN/Wi-Fi, DNS/NIC selection, DPI/display differences, sleep/reconnect and input latency without a shared virtualization host | The full installer matrix or every manual checklist item |

`buildNowSlim` retains installed testing for the other selected modules. The MWB
Sandbox suite uses protected lean copies of that **same Release build**; it does
not claim to execute MWB from the installed location. The guest's public test
certificate and non-console opt-in are isolated prerequisites, not production
installer changes.

The shared Release suite is the first automation priority. A small physical
two-PC smoke is valuable before a release because Sandbox's virtual switch and
controlled peer mapping do not exercise real network discovery or hardware.
Automating an entire physical-device matrix is a separate, more expensive task.
Neither the hidden setting nor an installed machine-wide build enables service,
locked-desktop or secure-desktop support in this pilot.
