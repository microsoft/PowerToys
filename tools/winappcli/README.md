# winappcli — UI test suite for PowerToys Command Palette

A PowerShell-only UI test suite that drives the PowerToys **Command Palette**
(CmdPal) module via Microsoft's
[winappCli](https://github.com/microsoft/winappCli) (Windows UI Automation
client) and asserts both the on-disk settings schema and live UI behaviour.

This folder is a focused example of the winappcli approach: a single,
fully-translated module checklist (Command Palette) plus the shared helper
module it depends on. It is intended as a template for porting the manual
release-checklist boxes of other modules into runnable assertions.

## Folder layout

```
winappcli\
├── README.md                                 (this file)
├── AAA-PATTERN-CONVENTION.md                 Arrange/Act/Assert/Cleanup convention
├── WinAppCli.PowerToys\                      shared helper module
│   ├── WinAppCli.PowerToys.psd1              manifest
│   ├── WinAppCli.PowerToys.psm1              entry point (dot-sources functions\)
│   └── functions\                            implementation (01–14)
└── modules\
    ├── _shared\Assertions.ps1                uniform Assert-* vocabulary
    ├── command-palette-checklist.ps1         CmdPal orchestrator (entry point)
    ├── command-palette-099-coverage-gaps.md  intentionally-deferred tests
    └── cmdpal\                               per-provider test files + helpers
        ├── 01-Bootstrap.tests.ps1 … 24-SettingsUI.tests.ps1
        └── helpers\                          CmdPal-specific helpers
```

## Prerequisites

1. **winappCli v0.3.1** (or newer):
   ```powershell
   winget install --id Microsoft.WinAppCli --source winget
   winapp --version                  # expect >= 0.3.1; reopen the shell if not on PATH
   ```
2. **PowerShell 7.2+** — the suite uses `#Requires -Version 7.0`:
   ```powershell
   winget install --id Microsoft.PowerShell --source winget
   pwsh -Version
   ```
3. **PowerToys** installed, with Command Palette (CmdPal 0.99+ ships as a
   bundled AppX) enabled:
   ```powershell
   winget install --id Microsoft.PowerToys --source winget
   # Then: PowerToys Settings → Command Palette → toggle ON
   Get-AppxPackage Microsoft.CommandPalette   # expect InstallLocation output
   ```
4. **Windows Search service** running (one Files test needs the indexer):
   ```powershell
   Get-Service WSearch               # expect Status='Running'
   ```

> The suite runs against an **installed** PowerToys (via winget / AppX); it does
> not build the product from this repo.

## Quick start

Smoke test to confirm the environment is wired up (< 1 second):

```powershell
cd <repo>\tools\winappcli
pwsh -File .\modules\command-palette-checklist.ps1 -Only 'CmdPal_Installed_*'
# expect: Report: PASS 1 · FAIL 0
```

Full suite (~15 min wall clock, AppX-state-dependent):

```powershell
pwsh -File .\modules\command-palette-checklist.ps1
```

Filtered / faster runs:

```powershell
pwsh -File .\modules\command-palette-checklist.ps1 -Only 'CmdPal_Calculator_*'
pwsh -File .\modules\command-palette-checklist.ps1 -Skip 'CmdPal_Stability_*'
pwsh -File .\modules\command-palette-checklist.ps1 -Tag schema      # CI gate, ~2s
pwsh -File .\modules\command-palette-checklist.ps1 -Tag list        # print the tag map
```

### Tag system

`-Tag` expands to a set of `-Only` patterns so test classes can be run without
remembering individual IDs:

| Tag | Meaning |
|---|---|
| `schema`      | pure file/JSON reads, no UI driving (~1s total) |
| `functional`  | provider e2e tests that drive CmdPal UI (~3 min) |
| `mutation`    | edit settings.json + restart AppX + verify (~80s) |
| `stability`   | regression guards (rapid typing, separator nav) |
| `integration` | PowerToys ↔ CmdPal integration tests |
| `pin`         | Dock pin tests |
| `bootstrap`   | install / settings-page / runtime verification |
| `ci`          | composite: `schema` + `bootstrap` |
| `nightly`     | composite: everything except destructive |

## Common gotchas

| Symptom | Cause | Fix |
|---|---|---|
| `winapp: command not found` | WinAppCli installed but PATH stale | Reopen the shell; or `$env:PATH += ';' + "$env:LOCALAPPDATA\Microsoft\WindowsApps"` |
| Many SKIPs with "CmdPal not found via list-windows" | CmdPal AppX not enabled / not running | PowerToys Settings → Command Palette → toggle on; or press Alt+Space |
| `Files_OpenActionForNonExecutable` fails with ENVIRONMENT-REQUIRED | Windows Search service stopped | `Start-Service WSearch` |
| Elevated PT + winappCli SendInput hits "access denied" | UIPI blocks elevated → non-elevated AppX | Run the test in non-elevated pwsh; or use `Send-PtKeyToWindow` (PostMessage path) |
| Stability test times out on first run | Cold AppX response latency | Re-run; warm AppX typically finishes in ~60s |

## How to debug a single test

Use the orchestrator's `-Only` filter (accepts wildcards and arrays):

```powershell
.\modules\command-palette-checklist.ps1 -Only 'CmdPal_SettingsUI_Dock_EnableDockShowsPowerDockWindow'
.\modules\command-palette-checklist.ps1 -Only 'CmdPal_Calculator_*','CmdPal_Settings_*'
```

Recommended breakpoint workflow is VS Code with the
[PowerShell extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.PowerShell):
open a test file (e.g. `modules\cmdpal\24-SettingsUI-e2e.ps1`), press **F9** on
a line, then run the `-Only` command in the integrated terminal. For non-VS-Code
workflows, drop a `Wait-Debugger` statement or use `Set-PSBreakpoint`.

## Test convention

All tests follow the Arrange / Act / Assert / Cleanup pattern and use the shared
`Assert-*` vocabulary from `modules\_shared\Assertions.ps1`. See
[`AAA-PATTERN-CONVENTION.md`](AAA-PATTERN-CONVENTION.md) for the full rationale,
and [`modules\command-palette-099-coverage-gaps.md`](modules/command-palette-099-coverage-gaps.md)
for tests intentionally deferred.
