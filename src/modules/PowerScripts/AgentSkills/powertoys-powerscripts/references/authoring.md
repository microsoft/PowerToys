# Discover, scaffold, validate

## Locate the engine and settings

Inspect the running PowerToys executable path and look beside it for
`PowerScripts\PowerScripts.Host.exe`. Otherwise check `%LOCALAPPDATA%\PowerToys`
or `%ProgramFiles%\PowerToys` with that suffix. Verify existence; resolve multiple
installations with the user. Do not scan the drive or download another Host.

```powershell
& $hostPath --help
& $hostPath consumers list
& $hostPath mxc-support --json
& $hostPath template list
```

These do not execute scripts. Check exits and stderr. Consumer enablement and
platform support do not establish task dependencies or installed UI capabilities.

Read `%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\config.json`.
Root precedence: explicit `--root` > `POWERSCRIPTS_ROOT` > `scriptsRoot` >
`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts`.
Report the chosen root. Read `python`/`mxc`; absent MXC preferences are
platform-derived. Never edit settings to bypass gates; configuration belongs in
PowerToys Settings, integration switches in PowerScripts app > Consumer modules.

## Scaffold the chosen contract

After agreeing on consumer, I/O, parameters, runtime, and side effects:

```powershell
$options = @{
    projectName = 'Clean copied text'
    description = 'Normalize copied text.'
    runtime = 'powershell'
    input = 'text'
    output = 'text'
    parameters = @()
} | ConvertTo-Json -Depth 10 -Compress
$createdJson = $options | & $hostPath template create --configure --root $scriptsRoot
if ($LASTEXITCODE -ne 0) { throw 'Creation failed; inspect stderr.' }
$created = $createdJson | ConvertFrom-Json
```

Replace the educational implementation with the task. The response gives
`entryPath`, `guidePath`; descriptor path is `entryPath + '.tool.json'`.
Creation neither grants trust nor configures bindings.

Options: `runtime` (`powershell`/`python`), `input`/`output`
(`none`/`text`/`files`/`image`/`audio`), `fileExtensions`, `minFiles`, `maxFiles`,
`parameters`. Creation's `maxFiles: 0` omits the schema maximum.

For a known PowerToys operation, inspect the operation entries returned by `template list`
instead of generating a wrapper script or selecting an executable yourself. For example,
`{"projectName":"Find file usage","input":"none","output":"text","operation":{"commandId":"filelocksmith.query"}}`
uses the same `template create --configure` command to create an editable `action.json`,
its standard descriptor and README. It exposes arguments without defaults and fixes the
catalogue's defaults. Explicit `operation.bindings` can choose typed constants or named
parameter references; exposed names must have matching standard `parameters` declarations.
Creation does not run or approve the operation. Native module deployment and the effective
global sandbox settings still determine whether it can run; never relax them automatically.
Keyboard Manager consumes the resulting ordinary script and needs its required values
configured before a hotkey can run silently.

A parameter entry has `name`, `schema`, and `required`, for example:

```json
{"name":"limit","schema":{"type":"integer","minimum":1,"default":10},"required":true}
```

Keep defaults typed. Required options need consumer collection or saved/default
values; an auxiliary file option does not replace primary `files` input.

## Validate without execution

```powershell
& $hostPath list --json --include-disabled --include-unconfigured --root $scriptsRoot
```

Registry errors may appear on stderr even with exit zero. Find the package by
`entryFullPath`; check opaque `id` (not title), runtime, `io`, schemas, defaults,
empty `configurationErrors`, and icon warnings.

Only with execution consent and normal trust, test `run <id> --consumer command-line`
(optional `--set name=value`) or `transform <id> --consumer command-line` with
stdin JSON. Never make `trust approve` an automatic setup step.
