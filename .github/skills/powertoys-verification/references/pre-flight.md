# Pre-flight checks, bootstrap, and state hygiene

This doc covers the **agent-runtime** environment probing and lifecycle hooks. Read alongside `SKILL.md` (the playbook) and `references/environment-setup.md` (one-time user env prep).

## Pre-flight checks (do these first; abort if any fails)

1. **Admin check** — `Test-PtAdmin` must return the elevation level matching `[ADMIN: YES]` items in the module's checklist. If the module contains `[ADMIN: YES]` items and `Test-PtAdmin` returns `False`, **STOP** and tell the user "this module requires an elevated session". Do NOT silently mark those items BLOCKED-LACK-ADMIN — that hides a fixable env issue.

2. **PT runner present** — `Test-PtRunnerAdmin` should show the runner exists. If it doesn't exist, start PowerToys (`Start-Process "$env:LOCALAPPDATA\PowerToys\PowerToys.exe"`).

3. **Module installed** — `Get-PtModuleSettings -ModuleDir <ModuleDir>` (or `Get-CmdPalSettings` for CmdPal) returns non-null.

4. **Interactive-desktop availability + session attachment** — the single most common cause of false-BLOCKED reports is a session mismatch where the agent runs in an elevated **non-console session** (e.g. RDP that's been disconnected/minimized, fast user switching, run-as-different-user, or scheduled-task-with-highest-privilege). In that scenario `Test-PtAdmin=True` but `GetForegroundWindow()=0` and `SendInput` returns `ERROR_ACCESS_DENIED (5)` — input injection cannot reach the active desktop.

   ```powershell
   # Sessions
   $agentSession   = [Diagnostics.Process]::GetCurrentProcess().SessionId
   $consoleSession = (Get-Process explorer -EA SilentlyContinue | Select-Object -First 1).SessionId
   "Agent session=$agentSession  Console explorer session=$consoleSession"

   # Foreground + Shell COM probe (use scripts/pt-session-diagnose.ps1 for the full version)
   Add-Type 'using System; using System.Runtime.InteropServices; public class FG4 { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); }'
   $hasFg = $false
   for ($i = 0; $i -lt 5; $i++) { if ([FG4]::GetForegroundWindow() -ne [IntPtr]::Zero) { $hasFg=$true; break }; Start-Sleep -Milliseconds 200 }
   $shellOk = $false
   try { $shellOk = (@((New-Object -ComObject Shell.Application).Windows()).Count -ge 0) } catch {}
   "Interactive desktop: ForegroundOk=$hasFg  ShellComOk=$shellOk"

   if (-not $hasFg -and $agentSession -ne $consoleSession) {
       Write-Host "===========================================================" -ForegroundColor Red
       Write-Host "NON-INTERACTIVE SESSION DETECTED" -ForegroundColor Red
       Write-Host "Agent is in Session $agentSession but the active console is Session $consoleSession." -ForegroundColor Red
       Write-Host "SendInput, global hotkeys, and arrow-key navigation will NOT work here." -ForegroundColor Red
       Write-Host "Items requiring input injection will be marked BLK-ENV up-front." -ForegroundColor Red
       Write-Host "Mitigation: see references/environment-setup.md, or relaunch in console session:" -ForegroundColor Yellow
       Write-Host "  psexec -accepteula -h -i $consoleSession -s pwsh.exe" -ForegroundColor Yellow
       Write-Host "===========================================================" -ForegroundColor Red
       # Continue verification — schema/UIA/CLI-based tests still produce real evidence
   }
   ```

   **Key distinction** (all rows assume `Admin=True`):
   - **ForegroundOk + ShellComOk** → Everything works — interactive elevated session.
   - **ShellComOk only (ForegroundOk false)** → Non-interactive (e.g. Session ≠ console, RDP minimized, screen locked, screensaver). Only schema / UIA-invoke / CLI / Named-Event tests work. Mark input-injection items as `BLK-ENV` and **cite `references/environment-setup.md` in the report** so the user can fix env and re-run.
   - **Neither (ShellComOk false)** → Session 0 / service context — even Shell COM fails. Very few tests possible.

5. **Discipline: try AT LEAST 2 distinct entry-paths before marking BLOCKED.** For Peek/FZ/Workspaces/Image Resizer/PowerRename/File Locksmith specifically, the obvious entry-path is the global hotkey but Shell.Application COM driving Explorer also works — see per-module profiles under `references/modules/`. Marking BLOCKED after trying only the CLI launch (a common trap) hides easily-PASS-able items in an interactive session.

## Bootstrap (paste at start of your verification script)

```powershell
$skill = '<this skill folder>'   # the folder containing SKILL.md
Get-ChildItem "$skill\scripts" -Filter '*.ps1' | ForEach-Object { . $_.FullName }

$workspace = "$env:TEMP\verify-<Module>-$(Get-Date -Format yyyyMMdd-HHmmss)"
New-Item -ItemType Directory -Path $workspace, "$workspace\artifacts" | Out-Null
$report = "$workspace\verify-<Module>.md"

"# <Module> verification — $(Get-Date -Format 'yyyy-MM-dd HH:mm')" | Set-Content $report
"" | Add-Content $report
"## Pre-flight" | Add-Content $report
"- IsAdmin: $(Test-PtAdmin)" | Add-Content $report
"- PT runner: PID=$((Test-PtRunnerAdmin).Pid) Elevated=$((Test-PtRunnerAdmin).Elevated)" | Add-Content $report

# Then proceed with pre-flight checks #4-#6 above and write their results into the report.
```

## State hygiene (CRITICAL — always restore)

Wrap any settings/registry mutation in try/finally:

```powershell
# Per-item: settings.json edits
$bk = Backup-PtModuleSettings -ModuleDir <ModuleDir>
try {
    # ... mutate + assert ...
} finally {
    Restore-PtModuleSettings -ModuleDir <ModuleDir> -BackupPath $bk
}

# After GPO/admin tests
Remove-Item HKLM:\Software\Policies\PowerToys -Recurse -Force -EA SilentlyContinue
Remove-Item HKCU:\Software\Policies\PowerToys -Recurse -Force -EA SilentlyContinue
Remove-Item 'C:\Windows\PolicyDefinitions\PowerToys.admx' -Force -EA SilentlyContinue
Remove-Item 'C:\Windows\PolicyDefinitions\en-US\PowerToys.adml' -Force -EA SilentlyContinue

# Spawned processes (notepad, regedit, etc.) — kill by PID, not by name
foreach ($pid in $spawnedPids) { Stop-Process -Id $pid -Force -EA SilentlyContinue }
```

## Final wrap-up (run AFTER all per-item tables are written)

1. **Run state-hygiene cleanup** above for everything that wasn't restored per-item.
2. **Write the top-of-report summary** per `references/reporting-format.md` §B.
3. **Write the §G Retrospective** — reflect on the run itself: every friction (classified by source + severity + minutes/attempts cost + suggested fix), or `Everything was smooth — no friction encountered.` See `references/reporting-format.md` §G. Don't skip it; it's how the skill improves.
4. **Verify every screenshot referenced in the report actually exists on disk** (before the move, while paths still resolve under `$workspace`):
   ```powershell
   $missing = Get-Content $report | Select-String 'artifacts/L\d+/step-\d+-[^\.\s]+\.(png|txt|log|json|ps1)' -AllMatches |
       ForEach-Object { $_.Matches.Value } | Sort-Object -Unique |
       Where-Object { -not (Test-Path (Join-Path $workspace $_)) }
   if ($missing) { Write-Warning "Missing artifacts: $($missing -join ', ')" }
   ```
5. **Move the workspace to the sign-off archive** (LAST step, after the report + artifact check pass):
   ```powershell
   $signoff = "$env:OneDrive\PowerToys\Module-Signoff"
   New-Item -ItemType Directory -Path $signoff -Force | Out-Null
   $final = Join-Path $signoff (Split-Path $workspace -Leaf)
   Move-Item -Path $workspace -Destination $final -Force
   $report = Join-Path $final (Split-Path $report -Leaf)
   ```
   The report uses **relative** `artifacts/…` paths, so the whole tree moves intact.
6. **Print the FINAL (moved) report path** as the very last line of your response — the `…\Module-Signoff\verify-<Module>-<timestamp>\verify-<Module>.md` path, NOT the temp path.

## Hard rules

- **Never silently send keys via SendInput** to a target window without first calling `Assert-PtForegroundOrAbort -AppId <id>`. Keys silently leak to your terminal if the target isn't foreground.
- **Never mark BLOCKED without trying at least 2 distinct entry-paths from the drive-stack** (SKILL.md §2). If you can't drive the item, name the specific obstacle (not "I can't").
- **Never assume any external repo is cloned locally.** The helpers under `scripts/` are self-contained. Use `Test-Path` guards before referencing any external path.
- **Never invent test steps for a `[CLARITY: VAGUE-*]` item** — mark it **FAIL (cause: checklist-ambiguous)** and quote the original wording so the user can fix the checklist. The checklist is test code; an undefinable test is a broken test.
- **Always restore state** before exiting (even on error). State hygiene wraps every mutation in try/finally.
- **Separate the two FAIL causes**: *product* FAILs are bugs to file; *checklist* FAILs (stale feature or ambiguous spec) are items to rewrite/prune. If a large share of a module's items are checklist-FAILs, the checklist needs an overhaul before re-verifying — don't punt drivable items into a FAIL.
- **Never continue past 3 consecutive errors against the same item** — mark it BLOCKED with the concrete symptom/obstacle and move on. Per-item budget is ~5 minutes; if stuck longer, it's BLOCKED (name the wall).
