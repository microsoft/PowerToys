# PowerDisplay CLI

Headless command-line interface for PowerDisplay. Drives the same DDC/CI and WMI controllers the GUI uses, so any monitor the tray app can adjust is also reachable from a script or terminal.

- **Binary**: `PowerToys.PowerDisplay.Cli.exe`
- **Install location**: `C:\Program Files\PowerToys\modules\PowerDisplay\` (shipped alongside `PowerToys.PowerDisplay.exe`)
- **Source**: [src/modules/powerdisplay/PowerDisplay.Cli/](../../../../src/modules/powerdisplay/PowerDisplay.Cli/)

## Commands at a glance

```
powerdisplay list
powerdisplay get          [-n <N> | -i <ID>] [--setting <name>]
powerdisplay set           -n <N> | -i <ID>   --<setting> <value>
powerdisplay capabilities  -n <N> | -i <ID>
```

Every subcommand accepts `--json` for machine-readable output and `--help` for inline reference.

## Monitor selector

Every value-bearing subcommand picks the target monitor with one of two flags:

| Flag | Alias | Type | Description |
|---|---|---|---|
| `--monitor-number` | `-n` | int | 1-based index, the same number shown by `list`. |
| `--monitor-id` | `-i` | string | Stable monitor ID (the Windows `DevicePath` with the trailing GUID stripped, e.g. `\\?\DISPLAY#DELD1A8#5&abc&0&UID12345`). Survives reboots and OS-level monitor reordering. |

Precedence rules:

- Neither flag → `get` and `capabilities` operate on **all** discovered monitors; `set` errors with exit code `6` (selector required).
- Only `-n` → resolve by `MonitorNumber`.
- Only `-i` → exact, case-insensitive match on `Monitor.Id`.
- **Both** → `-i` wins. A warning is printed to stderr noting that `-n` was ignored.
- Selector matches nothing → exit code `1` (`MONITOR_NOT_FOUND`).

## `list`

Discover attached monitors and print one row per monitor.

```
$ powerdisplay list
#  Name                   Method   Monitor ID
1  Dell U2723QE           DDC/CI   \\?\DISPLAY#DELD1A8#5&abc&0&UID12345
2  Built-in display       WMI      \\?\DISPLAY#BOE0900#4&def&0&UID111
```

No options. The same info is available as a JSON array via `--json`.

## `get`

Read the current values of one or all settings for one or all monitors.

```
$ powerdisplay get -n 1
Monitor 1 (Dell U2723QE)
  protocol           DDC/CI
  id                 \\?\DISPLAY#DELD1A8#5&abc&0&UID12345
  brightness         30%
  contrast           50%
  volume             70%
  color-temperature  6500K (0x05)
  input-source       HDMI-1 (0x11)
  power-state        On (0x01)
  orientation        0°
```

Variants:

| Invocation | Behaviour |
|---|---|
| `get` | All settings for **every** monitor (one section per monitor). |
| `get -n <N>` / `get -i <ID>` | All settings for **one** monitor. |
| `get -n <N> --setting <name>` | A **single** setting for one monitor. |
| `get --setting brightness` | A single setting across **all** monitors. |

`--setting` accepts: `brightness`, `contrast`, `volume`, `color-temperature`, `input-source`, `power-state`, `orientation`.

## `set`

Apply one setting to one monitor. Exactly one `--<setting>` flag must be provided; combining multiple is rejected with exit code `7`.

```
$ powerdisplay set -n 1 --brightness 50
Monitor 1 (Dell U2723QE) [DDC/CI]: brightness 30 → 50
```

### Continuous settings

| Flag | Type | Range | Backing VCP |
|---|---|---|---|
| `--brightness` | int | 0–100 | `0x10` (or WMI `WmiMonitorBrightness` on internal panels) |
| `--contrast` | int | 0–100 | `0x12` |
| `--volume` | int | 0–100 | `0x62` |

Values outside the range fail with exit code `2` (`OUT_OF_RANGE`) and an error message that quotes the accepted range.

### Discrete settings

Each accepts either the friendly name **or** the raw hex VCP value. Names are case-insensitive.

| Flag | Accepted names (examples) | Hex form | Backing VCP |
|---|---|---|---|
| `--color-temperature` | `sRGB`, `4000K`, `5000K`, `6500K`, `7500K`, `9300K`, `User 1`–`User 3` | `0x01`–`0x0D` | `0x14` |
| `--input-source` | `HDMI-1`, `HDMI-2`, `DisplayPort-1`, `DisplayPort-2`, `USB-C`, `DVI-1`, `VGA-1`, … | `0x01`–`0x1B` | `0x60` |
| `--power-state` | `On`, `Standby`, `Suspend`, `Off (DPM)`, `Off (Hard)` | `0x01`–`0x05` | `0xD6` |
| `--orientation` | `0`, `90`, `180`, `270` (degrees) | n/a | not VCP — uses Windows `ChangeDisplaySettingsEx` |

Examples:

```
powerdisplay set -n 1 --input-source HDMI-1
powerdisplay set -n 1 --input-source 0x11
powerdisplay set -n 2 --color-temperature 6500K
powerdisplay set -i "\\?\DISPLAY#DELD1A8#5&abc&0&UID12345" --power-state Standby
powerdisplay set -n 1 --orientation 90
```

If the value is unparseable or not advertised in the monitor's supported set, the CLI prints the supported values verbatim and exits with code `3`:

```
$ powerdisplay set -n 1 --input-source PIZZA
Error: --input-source value 'PIZZA' is not supported by Monitor 1 (Dell U2723QE)
  supported: HDMI-1 (0x11), HDMI-2 (0x12), DisplayPort-1 (0x0F), USB-C (0x1B)
  hint: pass a name from the list above, or a raw hex value like 0x11
```

If the monitor doesn't support the setting at all (e.g. an internal panel cannot do contrast), exit code is `4`:

```
$ powerdisplay set -n 2 --contrast 50
Error: Monitor 2 (Built-in display) does not support contrast adjustment
  hint: reason: internal panel exposes only brightness via WmiMonitorBrightness; DDC/CI capabilities are not available
```

## `capabilities`

Dump the parsed VCP capability set advertised by the monitor.

```
$ powerdisplay capabilities -n 1
Monitor 1 (Dell U2723QE) via DDC/CI
  Model: U2723QE
  MCCS:  2.2
  VCP codes:
    0x10 Brightness (continuous)
    0x12 Contrast (continuous)
    0x14 Select Color Preset: sRGB (0x01), 6500K (0x05), 7500K (0x06), User 1 (0x0B)
    0x60 Input Source: HDMI-1 (0x11), DisplayPort-1 (0x0F), USB-C (0x1B)
    0xD6 Power Mode: On (0x01), Standby (0x02), Off (DPM) (0x04)
  Raw: (raw MCCS string)
```

## Global options

| Flag | Effect |
|---|---|
| `--json` | Emit a stable JSON envelope instead of human-readable text. Goes on stdout; warnings and errors stay on stderr. |
| `--help` / `-h` / `-?` | Print help for the (sub)command. |
| `--version` | Print the CLI version. |

## Exit codes

| Code | Constant | Meaning |
|---|---|---|
| 0 | `Ok` | Success. |
| 1 | `MonitorNotFound` | Selector matched no monitor. |
| 2 | `OutOfRange` | Continuous value outside `[0, 100]`. |
| 3 | `InvalidDiscreteValue` | Discrete value unparseable or not in the monitor's supported set. |
| 4 | `UnsupportedFeature` | Monitor does not support this setting. |
| 5 | `HardwareFailure` | DDC/CI or WMI write returned failure. |
| 6 | `SelectorMissing` | `set` invoked without `-n`/`-i`. |
| 7 | `ArgumentError` | `System.CommandLine` parse failure, missing/duplicated `--<setting>`, or unknown setting name. |

Scripts can branch on the exit code rather than parsing strings.

## JSON output

The `--json` flag switches every command to a stable envelope. All keys are camelCase. `null`/missing fields are omitted.

### Success — `set`

```json
{
  "ok": true,
  "command": "set",
  "monitor": {
    "number": 1,
    "id": "\\\\?\\DISPLAY#DELD1A8#5&abc&0&UID12345",
    "name": "Dell U2723QE",
    "method": "DDC/CI"
  },
  "setting": "brightness",
  "beforeRaw": 30,
  "afterRaw": 50,
  "beforeDisplay": "30%",
  "afterDisplay": "50%"
}
```

### Success — `get` (single or all monitors share this shape)

```json
{
  "ok": true,
  "command": "get",
  "monitors": [
    {
      "monitor": { "number": 1, "id": "...", "name": "Dell U2723QE", "method": "DDC/CI" },
      "settings": [
        { "setting": "brightness", "raw": 30, "display": "30%", "supported": true },
        { "setting": "contrast",   "raw": 50, "display": "50%", "supported": true },
        { "setting": "input-source", "raw": 17, "display": "HDMI-1 (0x11)", "supported": true }
      ]
    }
  ]
}
```

### Success — `list`

```json
{
  "ok": true,
  "command": "list",
  "monitors": [
    {
      "number": 1,
      "id": "\\\\?\\DISPLAY#DELD1A8#...",
      "name": "Dell U2723QE",
      "method": "DDC/CI",
      "supportsBrightness": true,
      "supportsContrast": true,
      "supportsVolume": false,
      "supportsColorTemperature": true,
      "supportsInputSource": true,
      "supportsPowerState": true
    }
  ]
}
```

### Error

```json
{
  "ok": false,
  "command": "set",
  "monitor": { "number": 1, "id": "...", "name": "Dell U2723QE", "method": "DDC/CI" },
  "error": {
    "code": "INVALID_DISCRETE_VALUE",
    "exitCode": 3,
    "message": "--input-source value 'PIZZA' is not supported by Monitor 1 (Dell U2723QE)",
    "setting": "input-source",
    "requested": "PIZZA",
    "supported": [
      { "name": "HDMI-1", "vcp": "0x11" },
      { "name": "HDMI-2", "vcp": "0x12" },
      { "name": "DisplayPort-1", "vcp": "0x0F" },
      { "name": "USB-C", "vcp": "0x1B" }
    ],
    "hint": "pass a name from the list above, or a raw hex value like 0x11"
  }
}
```

Error codes (the `error.code` string): `MONITOR_NOT_FOUND`, `OUT_OF_RANGE`, `INVALID_DISCRETE_VALUE`, `UNSUPPORTED_FEATURE`, `HARDWARE_FAILURE`, `SELECTOR_MISSING`, `ARGUMENT_ERROR`.

## Logging

Beyond stdout/stderr, the CLI writes a rotating log to `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\` via `ManagedCommon.Logger`. This is shared with the GUI module, so DDC/CI errors surfaced by the controllers are recoverable post-mortem.

## Scripting recipes

Set brightness on every external monitor at once (PowerShell):

```powershell
$monitors = (powerdisplay list --json | ConvertFrom-Json).monitors |
            Where-Object { $_.method -eq 'DDC/CI' }
foreach ($m in $monitors) {
    powerdisplay set -i $m.id --brightness 60
}
```

Read brightness for all monitors and emit `(number, name, brightness)` rows (PowerShell):

```powershell
(powerdisplay get --setting brightness --json | ConvertFrom-Json).monitors |
    ForEach-Object {
        [pscustomobject]@{
            Number     = $_.monitor.number
            Name       = $_.monitor.name
            Brightness = $_.settings[0].raw
        }
    } | Format-Table
```

Fail a CI step only on real hardware failures, not on missing monitors:

```bash
powerdisplay set -n 1 --brightness 50
case $? in
  0) echo "ok";;
  1) echo "monitor not found, skipping"; exit 0;;
  *) echo "hard failure"; exit 1;;
esac
```
