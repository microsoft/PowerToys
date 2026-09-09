# Light Switch CLI

`PowerToys.LightSwitch.CLI.exe` queries and controls the running Light Switch
service in the current Windows user session. Enable Light Switch in PowerToys
before using it. The CLI does not start PowerToys or enable the module.

On its first startup, the service creates the default settings if the module's
settings file does not exist: scheduling is off and both theme targets are
enabled. Existing, malformed, or unreadable files are never replaced with
defaults. This also supports enabling the module through preconfigured general
settings or policy before visiting the settings page.

The PATH-visible executable is a [CLI shim](../cli-conventions.md). Its target,
`PowerToys.LightSwitch.Cli.exe`, is installed in the PowerToys installation root.
Terminals opened before installation may need to be reopened to pick up PATH.

## Commands

```console
PowerToys.LightSwitch.CLI.exe status
PowerToys.LightSwitch.CLI.exe status --json
PowerToys.LightSwitch.CLI.exe light
PowerToys.LightSwitch.CLI.exe dark
PowerToys.LightSwitch.CLI.exe toggle
PowerToys.LightSwitch.CLI.exe schedule disable
PowerToys.LightSwitch.CLI.exe schedule enable --mode fixed-hours
PowerToys.LightSwitch.CLI.exe schedule enable --mode sunset-to-sunrise
PowerToys.LightSwitch.CLI.exe schedule enable --mode follow-night-light
```

`light` and `dark` set the targets selected in Light Switch settings (system,
applications, or both). Repeating the same command does not toggle the manual
override flag. A theme different from the current automatic plan is held using
Light Switch's existing manual override behavior, until a scheduled boundary or
a relevant settings change.

`toggle` uses the same service operation as the shortcut. It retains the existing
behavior: invert each selected target, toggle manual override, then evaluate the
schedule. In particular, mixed system/application themes can be reconciled to
the automatic plan when the operation releases an existing override.

`schedule disable` saves the existing `scheduleMode` property as `Off`.
`schedule enable --mode` selects one of the existing modes and uses the values
already saved for its times, coordinates, and offsets. It changes only the
schedule mode; it does not reset time values or record a previous mode.

Without `--mode`, `schedule enable` succeeds without changing an already-enabled
schedule. If the mode is `Off`, it fails with exit code 2 and asks for an explicit
mode. Re-selecting the current mode does not clear a manual theme override.

All commands support `--json`. `--help` and `--version` work without the service.

## Output and exit codes

Successful service responses use a versioned JSON envelope:

```json
{
  "version": 1,
  "success": true,
  "state": {
    "systemTheme": "dark",
    "appsTheme": "dark",
    "changeSystem": true,
    "changeApps": true,
    "scheduleMode": "FixedHours",
    "manualOverride": true
  }
}
```

The mode is the service's accepted configuration, not its last applied-mode
cache. System and application themes are read separately. Reading a theme can
fail; an error response may include a state with that value set to `unknown`.

Errors contain `version`, `success: false`, and an `error` object with stable
`code` and human-readable `message` fields. When available, `state` describes the
observed result, including partial theme changes. JSON output is one object on
stdout. Human-readable errors go to stderr; logs do not mix with JSON.

| Exit code | Meaning |
| --- | --- |
| 0 | The command completed successfully. |
| 1 | Execution, theme access, or protocol failure. |
| 2 | Invalid arguments or invalid configuration. |
| 3 | The service is unavailable or cannot accept the request. |
| 4 | Timeout or a lost connection after sending a request; the result may be unknown. |

The shim can additionally return its documented 9009-9011 startup errors.

A successful schedule command has saved the mode and completed the service's
settings handling, including any required Night Light observer lifecycle change.
Observer initialization runs asynchronously. The settings window
continues to refresh through its existing file watcher. Concurrent settings
editors retain the existing whole-file save behavior: a later save, including a
stale UI snapshot, can replace an earlier update. The CLI does not add field-level
conflict merging.

A request timeout does not roll back Windows changes. The CLI never
automatically retries a mutation, especially `toggle`. Query status before
deciding whether to issue another command. Windows theme broadcasts can take
time; successful theme application does not mean every application has finished
redrawing or that a downstream PowerDisplay profile has completed.

## Implementation

The C# CLI parses arguments with the repository's pinned System.CommandLine
package. It connects to the service through a duplex named pipe, verifies the
server executable, sends one request, reads one response, and closes.

The pipe name is `PowerToys_LightSwitch_Cli_<sessionId>`. Its access control
restricts callers to the service's user/logon scope and rejects remote clients.
After reading a bounded frame, the service obtains the connection's identification
token and verifies its user SID, desktop session, and logon SID. This accepts the
same user's linked UAC tokens while rejecting independent logons. The service
reverts to its own identity before parsing or executing commands.
Wire messages are single-line JSON encoded as BOM-less UTF-16LE, with a maximum
of 32,768 UTF-16 code units per request or response. Version 1 requests contain
`version`, `command`, and, only for `schedule-enable`, an optional `mode`.

Wire command names are `status`, `light`, `dark`, `toggle`, `schedule-enable`,
and `schedule-disable`. Wire modes use the existing setting values:
`FixedHours`, `SunsetToSunrise`, and `FollowNightLight`. Unknown versions,
commands, modes, and unexpected properties are rejected before execution.

Theme commands and snapshots use the existing StateManager mutex. A command
uses one configuration snapshot throughout its theme application and state
update. Schedule requests are handed to the existing service worker because it
owns the Night Light observer's lifetime. Observer joins never run while holding
the StateManager lock.

The shortcut submits a counted toggle request to the service instead of
modifying Windows and sending a delayed completion notification. The Command
Palette's existing toggle event still reaches that shortcut adapter. This
preserves the number of requests received by the module and avoids an old
completion event cancelling a newer CLI override.

## Validation

The managed tests cover argument parsing, text/JSON output, exit codes, bounded
pipe reads, server identity, and response validation. Native tests cover strict
request parsing, pipe lifetime/framing, theme operations with fake Windows
dependencies, and settings handling without changing the desktop theme.

Manual acceptance should include all three target selections, mixed themes,
shortcut/CLI sequences, Off-to-mode transitions, all three automatic modes,
delayed settings notifications, settings read/write failures, and stopping the
module while a client is connected.
