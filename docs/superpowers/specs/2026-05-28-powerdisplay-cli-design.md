# PowerDisplay CLI — Design

**Date:** 2026-05-28
**Branch:** `yuleng/pd/cli/1`

## Goal

Ship a headless command-line interface for PowerDisplay that can:

1. List attached monitors (number + stable ID + name + transport).
2. Configure continuous settings (brightness, contrast, volume) by percentage.
3. Configure discrete settings (color temperature, input source, power state, orientation) by symbolic name or hex value.
4. Identify the target monitor by **monitor number** (`-n`) or **stable monitor ID** (`-i`). If both are supplied, monitor ID wins.
5. Validate every input and emit a *specific reason* when validation fails — unsupported feature, value out of range, value not in the supported discrete set.

## Non-goals

- Batching multiple settings in one invocation (`set --brightness 50 --contrast 60`). Reserved for a follow-up because per-setting failure semantics need their own design.
- Profile management (`apply-profile`, `save-profile`). The CLI is for single-shot operations; profile management remains the GUI's responsibility.
- Watching/polling/streaming.
- Localisation. CLI output is English-only.

## Packaging

A **new console project** `src/modules/powerdisplay/PowerDisplay.Cli/` producing `PowerToys.PowerDisplay.Cli.exe`. Console executable (`<OutputType>Exe</OutputType>`), AOT-compatible (`<PublishAot>true</PublishAot>`), no WinUI dependency. Output goes to the same `WinUI3Apps` directory as `PowerDisplay.exe`.

**Refactor:** `MonitorManager.cs` moves from `PowerDisplay/Helpers/MonitorManager.cs` to `PowerDisplay.Lib/Services/MonitorManager.cs` (namespace `PowerDisplay.Common.Services`). Verified by inspection: zero WinUI dependencies. The CLI and the WinUI app both consume the same orchestration layer, so identical blacklist filtering, internal/external classification, and controller dispatch apply to both. Five existing call sites in PowerDisplay.exe get `using PowerDisplay.Common.Services;` instead of `using PowerDisplay.Helpers;`.

## Command surface

```
powerdisplay list
powerdisplay capabilities -n <N>|-i <ID>
powerdisplay get        -n <N>|-i <ID> [--setting <name>]
powerdisplay set        -n <N>|-i <ID> --<setting> <value>
```

Verbs are `System.CommandLine` subcommands so each gets its own scoped `--help`. `--json` is a global option that switches to machine-readable output on any subcommand.

## Monitor selector (`MonitorResolver`)

A shared resolver owns the precedence rules:

| -n  | -i  | Behaviour |
|-----|-----|-----------|
| ❌  | ❌  | Error `BOTH_SELECTORS_MISSING` (exit 6); message points to `powerdisplay list` |
| ✓   | ❌  | Find first monitor with `MonitorNumber == n` |
| ❌  | ✓   | Find monitor with `Id` exactly matching (case-insensitive) |
| ✓   | ✓   | Use `-i`. Print `warning: --monitor-number <n> ignored because --monitor-id was also provided` to stderr |
| any | any (no match) | Error `MONITOR_NOT_FOUND` (exit 1) |

## Settings

| Flag | Kind | Backing call | Capability check | Value parsing | Range/set check |
|---|---|---|---|---|---|
| `--brightness <0-100>` | continuous | `MonitorManager.SetBrightnessAsync` | `monitor.SupportsBrightness` | `int.Parse` | `[0, 100]` |
| `--contrast <0-100>` | continuous | `SetContrastAsync` | `monitor.SupportsContrast` | `int.Parse` | `[0, 100]` |
| `--volume <0-100>` | continuous | `SetVolumeAsync` | `monitor.SupportsVolume` | `int.Parse` | `[0, 100]` |
| `--color-temperature <name\|hex>` | discrete | `SetColorTemperatureAsync` | `monitor.SupportsColorTemperature` | name (`6500K`, `sRGB`) or hex (`0x05`) via `VcpNames` reverse lookup | must be in `VcpCapabilitiesInfo.GetSupportedValues(0x14)` |
| `--input-source <name\|hex>` | discrete | `SetInputSourceAsync` | `monitor.SupportsInputSource` | name (`HDMI-1`, `USB-C`) or hex (`0x11`) | must be in `monitor.SupportedInputSources` |
| `--power-state <name\|hex>` | discrete | `SetPowerStateAsync` | `monitor.SupportsPowerState` | name (`On`, `Standby`, `Off-DPM`, `Off-Hard`) or hex (`0x01`) | must be in `monitor.SupportedPowerStates` |
| `--orientation <0\|90\|180\|270>` | discrete | `SetRotationAsync` | `!string.IsNullOrEmpty(monitor.GdiDeviceName)` | degrees | exactly one of `{0, 90, 180, 270}` |

Orientation accepts degree integers on the wire (`0`/`90`/`180`/`270`) — what users think in — and the resolver maps to the internal `0`/`1`/`2`/`3` index for `DisplayRotationService`.

## Validation flow (`set`)

```
1. Resolve selector             → MONITOR_NOT_FOUND (1) or BOTH_SELECTORS_MISSING (6)
2. Capability check             → UNSUPPORTED_FEATURE (4) with monitor-specific reason
3. Value parse                  → INVALID_DISCRETE_VALUE (3) for unparseable name/hex
                                  OUT_OF_RANGE (2) for continuous outside [0, 100]
4. Supported-set check (discrete only)
                                → INVALID_DISCRETE_VALUE (3); message lists supported names+hex
5. Apply via MonitorManager     → HARDWARE_FAILURE (5) wrapping MonitorOperationResult.ErrorMessage
```

Every error carries (a) a stable code, (b) a human-readable sentence stating *what was wrong*, (c) context about the monitor, and where relevant (d) the supported set or accepted range.

## Exit codes

| Code | Constant | Meaning |
|------|----------|---------|
| 0 | `Ok` | success |
| 1 | `MonitorNotFound` | selector matched no monitor |
| 2 | `OutOfRange` | continuous value outside `[0, 100]` |
| 3 | `InvalidDiscreteValue` | discrete value unparseable or not in supported set |
| 4 | `UnsupportedFeature` | monitor does not support this setting |
| 5 | `HardwareFailure` | DDC/CI or WMI write returned failure |
| 6 | `SelectorMissing` | required `-n`/`-i` not provided |
| 7 | `ArgumentError` | `System.CommandLine` parse failure |

## Output

### Text (default, human-readable)

```
$ powerdisplay list
#  Monitor ID                                          Name              Method
1  \\?\DISPLAY#DELD1A8#5&abc&0&UID12345                Dell U2723QE      DDC/CI
2  \\?\DISPLAY#BOE0900#4&...&UID111                    Built-in display  WMI

$ powerdisplay set -n 1 --brightness 50
Monitor 1 (Dell U2723QE): brightness 30 → 50

$ powerdisplay set -n 1 --brightness 150
Error: --brightness value 150 is out of range
  expected: integer in [0, 100]
  monitor: Monitor 1 (Dell U2723QE)

$ powerdisplay set -n 1 --input-source PIZZA
Error: --input-source value 'PIZZA' is not supported by Monitor 1 (Dell U2723QE)
  supported: HDMI-1 (0x11), HDMI-2 (0x12), DisplayPort-1 (0x0F), USB-C (0x1B)
  hint: pass a name from the list above, or a raw hex value like 0x11

$ powerdisplay set -n 2 --contrast 50
Error: Monitor 2 (Built-in display) does not support contrast adjustment
  reason: internal panel exposes only brightness via WmiMonitorBrightness; DDC/CI capabilities are not available
```

### JSON (`--json`)

Stable envelope on every command:

```json
{
  "ok": true,
  "command": "set",
  "monitor": { "number": 1, "id": "\\\\?\\DISPLAY#DELD1A8#...", "name": "Dell U2723QE" },
  "setting": "brightness",
  "before": 30,
  "after": 50
}
```

On error:

```json
{
  "ok": false,
  "command": "set",
  "error": {
    "code": "INVALID_DISCRETE_VALUE",
    "message": "input source 'PIZZA' is not supported by Monitor 1 (Dell U2723QE)",
    "setting": "input-source",
    "requested": "PIZZA",
    "supported": [
      { "name": "HDMI-1", "vcp": "0x11" },
      { "name": "HDMI-2", "vcp": "0x12" }
    ]
  }
}
```

JSON serialisation goes through a source-generated `JsonSerializerContext` to stay AOT-safe.

## File layout

```
src/modules/powerdisplay/PowerDisplay.Cli/
├── PowerDisplay.Cli.csproj
├── Program.cs                        # Main → root command
├── Commands/
│   ├── PowerDisplayRootCommand.cs
│   ├── ListCommand.cs
│   ├── CapabilitiesCommand.cs
│   ├── GetCommand.cs
│   └── SetCommand.cs
├── Options/
│   ├── MonitorSelectorOptions.cs     # shared -n / -i / --json
│   ├── ContinuousSettingOptions.cs   # --brightness / --contrast / --volume
│   └── DiscreteSettingOptions.cs     # --color-temperature / --input-source / --power-state / --orientation
├── Resolution/
│   ├── MonitorResolver.cs            # selector → Monitor or CliError
│   ├── DiscreteValueResolver.cs      # name|hex → VCP int + supported-set check
│   └── OrientationResolver.cs        # 0|90|180|270 → 0|1|2|3
├── Output/
│   ├── ICliOutput.cs
│   ├── TextCliOutput.cs
│   ├── JsonCliOutput.cs
│   ├── CliJsonContext.cs             # source-gen JsonSerializerContext
│   └── CliExitCodes.cs
└── Errors/
    └── CliError.cs                   # record CliError(string Code, string Message, IDictionary<string,object?> Details)

src/modules/powerdisplay/PowerDisplay.Cli.UnitTests/
├── PowerDisplay.Cli.UnitTests.csproj
├── MonitorResolverTests.cs
├── DiscreteValueResolverTests.cs
├── OrientationResolverTests.cs
└── ContinuousValueValidatorTests.cs
```

## Test strategy

MSTest matching `PowerDisplay.Lib.UnitTests`. Coverage targets:

- `MonitorResolverTests`: neither selector / only `-n` / only `-i` / both given (precedence + warning) / no match
- `DiscreteValueResolverTests`: name parsing (case-insensitive), hex parsing (`0x11` / `0X11` / `11`), unknown name, supported-set rejection (returns supported list)
- `OrientationResolverTests`: each valid degree mapped to index, `45` rejected with the accepted-set message
- `ContinuousValueValidatorTests`: boundaries (0, 100), -1, 101

Real-hardware integration tests are out of scope.

## Risks

- **AOT trim of `System.CommandLine`**: ImageResizer's project already uses it under AOT (PowerDisplay project itself is AOT). Confirm during build that no trim warnings escape.
- **JSON source-gen friction**: `CliError.Details` is `IDictionary<string,object?>`. AOT-safe source-gen prefers strongly-typed records — may need a small per-error-shape record set instead. Acceptable scope cost.
- **MonitorManager move**: 5 callers update. Reviewed in advance — all are `using` statement changes only.

## Open questions (deferred)

- Should `set --brightness 50 --contrast 60` be allowed in one call? **Deferred:** per-setting partial-failure semantics need separate design.
- Should the CLI auto-launch the PowerToys runner if it's not running? **No** — CLI is independent of the GUI's lifetime; it does its own discovery.
