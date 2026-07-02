# PowerDisplay CLI

`PowerToys.PowerDisplay.Cli.exe` is a headless command-line front end for controlling monitor
settings (brightness, contrast, volume, color temperature, input source, power state, orientation)
and applying saved profiles.

The examples below use `powerdisplay` as shorthand — that is the name the tool uses for itself in
its `--help` output and error hints. There is no separate `powerdisplay` shim today; invoke the
executable by its real name (`PowerToys.PowerDisplay.Cli.exe`) or via your own alias.

## How it works

The CLI is a thin client. It does **not** talk to the hardware directly: it connects to the running
PowerDisplay app over a per-session named pipe (`PipeNames.CliServer()`), sends one JSON request,
and renders the one JSON response the app returns.

- **The PowerDisplay module must be enabled and running.** If it is not, the CLI exits with `10`
  (`PROVIDER_UNAVAILABLE`) after a short connect timeout.
- The pipe is ACL'd to the current user's SID, so a non-elevated CLI can drive a same-user elevated
  app (and other users are denied). See `PowerDisplay/Ipc/CliPipeServer.cs`.
- One invocation is bounded by an overall deadline (`Program.OperationTimeout`, 5s); the connect
  phase is bounded separately and shorter (`Program.ConnectTimeout`, 2s) so a not-running app fails
  fast and correctly as `PROVIDER_UNAVAILABLE` rather than `TIMEOUT`.

Human-readable text goes to **stdout** (success) and **stderr** (warnings/errors). Scripts should
branch on the **process exit code** (below), which is the stable machine contract.

## Commands

Canonical names live in `PowerDisplay.Contracts/Requests/CliCommandNames.cs`.

| Command | Purpose | Selector |
|---|---|---|
| `list` | Discover attached monitors (number, id, name, transport). | none |
| `get` | Read the current value of one or all settings. | optional (omit = all monitors) |
| `set` | Apply exactly one setting to a monitor. | required |
| `up` / `down` | Raise / lower one continuous setting relative to its current value. | required |
| `capabilities` | Print the monitor's advertised VCP capabilities. | required |
| `profiles` | List saved profiles (name, monitor count, last modified). | none |
| `apply-profile <name>` | Apply a saved profile's per-monitor settings. | none |

### Selecting a monitor

- `-n`, `--monitor-number <n>` — 1-based index from `list`.
- `-i`, `--monitor-id <id>` — stable id from `list`. **Wins** if both are supplied (the CLI prints a
  note that `-n` was ignored).

### Settings

Names live in `PowerDisplay.Contracts/CliSettingNames.cs`.

| Setting | `set` flag | Kind | Value |
|---|---|---|---|
| brightness | `--brightness <0-100>` | continuous | percent |
| contrast | `--contrast <0-100>` | continuous | percent |
| volume | `--volume <0-100>` | continuous | percent |
| color-temperature | `--color-temperature <0xNN>` | discrete | hex VCP value |
| input-source | `--input-source <0xNN>` | discrete | hex VCP value |
| power-state | `--power-state <0xNN>` | discrete | hex VCP value |
| orientation | `--orientation <0\|90\|180\|270>` | GDI | degrees |

- Discrete values are **hex only** (e.g. `0x05`); friendly names are not accepted because the generic
  VCP name table can disagree with a specific panel. Run `capabilities --setting <name>` to list the
  values a monitor actually advertises.
- `set` requires **exactly one** setting flag.
- `up`/`down` accept one of `--brightness` / `--contrast` / `--volume` as a **no-value presence flag**,
  plus optional `--step <n>` (defaults to the PowerDisplay `mouse_wheel_increment` setting).
- Applying a `--power-state` that blanks the panel requires `--confirm-power-off`.

### Global options

- `--quiet` — suppress warning messages on stderr.

## Exit codes

Single source of truth: `PowerDisplay.Contracts/CliExitCodes.cs` (paired 1:1 with the `error.code`
strings in `CliErrorCodes.cs`). **This scheme extends the baseline in
[`../../cli-conventions.md`](../../cli-conventions.md); exit code `2` here means "out of range", not
"invalid arguments".**

| Exit | `error.code` | Meaning |
|---|---|---|
| 0 | — | Success |
| 1 | `MONITOR_NOT_FOUND` | The selected monitor number/id was not found. |
| 2 | `OUT_OF_RANGE` | A continuous value was outside `[0, 100]`. |
| 3 | `INVALID_DISCRETE_VALUE` | A discrete or orientation value was invalid, or not in the monitor's advertised set. |
| 4 | `UNSUPPORTED_FEATURE` | The monitor does not support the requested setting. |
| 5 | `HARDWARE_FAILURE` | The DDC/CI or GDI write failed. |
| 6 | `SELECTOR_MISSING` | A command that needs a monitor was given none. |
| 7 | `ARGUMENT_ERROR` | Invalid arguments (unknown setting, bad combination, parse error). |
| 8 | `TIMEOUT` | The operation exceeded the deadline or was cancelled (Ctrl+C). |
| 9 | `INTERNAL_ERROR` | Unexpected failure. |
| 10 | `PROVIDER_UNAVAILABLE` | The PowerDisplay app is not running / unreachable. |

For `apply-profile`, the exit code is the **worst** per-setting outcome across all monitors
(`HARDWARE_FAILURE` > `INVALID_DISCRETE_VALUE` > `OUT_OF_RANGE` > success); `unsupported` settings do
not fail the command.

## Examples

```pwsh
# List monitors
powerdisplay list

# Read everything for monitor 1
powerdisplay get -n 1

# Read just brightness for a specific monitor id
powerdisplay get -i "\\?\DISPLAY#..." --setting brightness

# Set brightness to 60% on monitor 2
powerdisplay set -n 2 --brightness 60

# Nudge volume down by 5
powerdisplay down -n 1 --volume --step 5

# Discover the color-temperature values a monitor advertises, then set one
powerdisplay capabilities -n 1 --setting color-temperature
powerdisplay set -n 1 --color-temperature 0x05

# Power the panel off (requires explicit confirmation)
powerdisplay set -n 1 --power-state 0x04 --confirm-power-off

# Apply a saved profile
powerdisplay apply-profile "Night"
```

## Related source

- CLI client: `src/modules/powerdisplay/PowerDisplay.Cli/`
- Shared contracts / DTOs: `src/modules/powerdisplay/PowerDisplay.Contracts/`
- App-side IPC (pipe server, executors, projectors): `src/modules/powerdisplay/PowerDisplay/Ipc/`
