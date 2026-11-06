# Discover the installed engine and create a package

## Find the Host

If PowerToys is running, inspect its executable path (read-only) and look for
`PowerScripts\PowerScripts.Host.exe` beside `PowerToys.exe`. For a normal
per-user installation, also check
`%LOCALAPPDATA%\PowerToys\PowerScripts\PowerScripts.Host.exe`; for a machine
installation, check `%ProgramFiles%\PowerToys\PowerScripts\PowerScripts.Host.exe`.
Verify the file exists. If several installations exist, ask which to use.
Do not recursively scan the user's drive, download another Host, or hardcode a
developer worktree. If not found, guide installation/repair.

Use PowerShell's call operator with the resolved full path:

```powershell
& $hostPath --help
& $hostPath consumers list
& $hostPath mxc-support --json
& $hostPath template list
```

Check exit codes and stderr. These commands do not execute user scripts.
`consumers list` reports effective enablement and policy, not all runtime settings.

## Resolve configuration

Read `%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\config.json` if it exists.
The configured folder is `scriptsRoot`; otherwise the default is
`%LOCALAPPDATA%\Microsoft\PowerToys\PowerScripts\scripts`.
`POWERSCRIPTS_ROOT` overrides the configured folder, and explicit `--root` wins
over both. Report the chosen root; never silently create in another location.
Read `python` and `mxc` settings to assess runtime/isolation. Use the Host's
support probe for platform capability; absent MXC preferences are platform-derived,
not permission to choose your own defaults.

The Host shares persisted configuration with Settings. Never edit it directly
to enable modules, change runtime/isolation, or bypass a managed preference.
Guide the user to **PowerToys Settings > PowerScripts** instead.

## Scaffold without execution

First finish the consumer, I/O, parameter, and runtime decisions in `SKILL.md`.
Map those decisions to creation options; do not always choose a no-input action
or use the VPN example as the default. For example, an Advanced Paste text
cleanup uses `input: text` and `output: text`, while Explorer report generation
requires `input: files` and an output its installed adapter can export.

Use a configured template to generate a unique package. This example scaffolds
only the shape of a no-input action; its feasibility must already be established:

```powershell
$options = @{
    projectName = 'Ensure VPN connected'
    description = 'Ensure the selected VPN profile is connected.'
    runtime = 'powershell'
    input = 'none'
    output = 'none'
    parameters = @()
} | ConvertTo-Json -Depth 10 -Compress
$createdJson = $options | & $hostPath template create --configure --root $scriptsRoot
if ($LASTEXITCODE -ne 0) { throw 'PowerScripts package creation failed; inspect stderr.' }
$created = $createdJson | ConvertFrom-Json
```

The response contains `entryPath` and `guidePath`. The descriptor is
`entryPath + '.tool.json'`. Creation produces an educational example; replace its
marked task with real, reviewed implementation. It does not configure a VPN,
grant trust, or assign a shortcut.

Creation options support `runtime` (`powershell`/`python`), `input` and `output`
(`none`/`text`/`files`/`image`/`audio`), `fileExtensions`, `minFiles`, `maxFiles`,
and `parameters`. Creation's `maxFiles: 0` means unlimited.
A parameter entry has `name`, `schema`, and `required`, for example:

```json
{"name":"profile","schema":{"type":"string","minLength":1},"required":true}
```

Parameters for a non-interactive hotkey need safe defaults or an explicitly
configured binding supplying them. Do not promise that every consumer prompts
for required parameters.

Choose parameters for task configuration, not as a substitute for primary input.
An auxiliary `file-path` option does not make a script accept an Explorer
selection. Preserve typed defaults (including false/zero/empty string).
Use file extension/count limits and media contracts appropriate to the task,
not merely whatever makes a script appear in a menu.

## Validate and hand off

```powershell
& $hostPath list --json --include-disabled --include-unconfigured --root $scriptsRoot
```

Inspect stdout **and stderr**: registry errors can be written to stderr even
when the command exits zero. Locate the created package by `entryFullPath`;
confirm its returned opaque `id`, runtime, `io`, empty `configurationErrors`,
schemas, parameter requirements/defaults, and expected paths. Compare them with
the plan and the selected consumer's profile. Check optional icon warnings too.
Do not use the friendly
title as the invocation ID. Discovery may list untrusted scripts; it grants no
execution approval and does not prove the code's behavior.

Only after explicit approval and normal trust requirements, an agent test can
use `run <id> --consumer command-line` (optionally `--set name=value`) or
`transform <id> --consumer command-line` with a JSON request on stdin.
Never use `trust approve` as an automatic setup step.
