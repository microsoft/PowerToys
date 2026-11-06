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
Parameter entries use `name`, `schema`, `required`:

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
