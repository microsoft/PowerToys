# Environment setup for PowerToys verification

**Audience**: human user preparing a test machine before running a verification agent.
**One-time** (per test session) — restore afterward.

## Why this matters

PowerToys release checklists test real user interactions: pressing hotkeys, dragging files, switching windows. Many tests use `SendInput` to inject keystrokes. Windows refuses `SendInput` when the calling session has **no attached input desktop** — and several common Windows states cause exactly that to happen:

- RDP client minimized
- Workstation locked (screensaver kicked in, idle timeout)
- Remote machine asleep
- Local machine asleep (RDP TCP drops)

If any of these happens mid-verification, items that need synthetic input fail with `BLK-ENV` even though the feature itself works fine. This guide eliminates the env causes so the only BLOCKED verdicts you see are real test/framework limitations.

## Per-scenario reference table

| Scenario | Remote session State | `GetForegroundWindow()` | `SendInput` | Verdict for input-injection tests |
|---|---|---|---|---|
| mstsc window focused | Active | Real HWND | Works | ✅ Drivable |
| mstsc visible but not focused (covered or alt-tabbed) | Active | Real HWND | Works | ✅ Drivable |
| **mstsc MINIMIZED** | Active | **0** | **ACCESS_DENIED (5)** | ❌ BLK-ENV |
| Local machine sleeps / RDP TCP drops | **Disconnected** | 0 | ACCESS_DENIED | ❌ BLK-ENV |
| User closes mstsc with X (no signout) | **Disconnected** | 0 | ACCESS_DENIED | ❌ BLK-ENV |
| Sign out from the remote | Session destroyed | — | — | ❌ Agent killed |
| Remote machine sleeps | Suspended | — | — | ❌ Catastrophic — timing corruption |
| Remote screensaver / auto-lock kicks in | Active but desktop locked | 0 | ACCESS_DENIED | ❌ BLK-ENV |
| **2nd RDP login as the SAME user** (you reconnect from another client) | the OLD session flips to **Disconnected** | 0 (in the old session) | ACCESS_DENIED | ❌ BLK-ENV — your running test's session got taken over |

**Key insight**: "Active" in `quser` ≠ "can inject input". Always check `GetForegroundWindow()` first (the diagnostic script `scripts/pt-session-diagnose.ps1` does this).

## Can I verify two modules at once in two RDP sessions?

Short answer on a **client edition of Windows (Windows 10/11, ProductType=1)**: **no — not as the same user, and effectively not at all.** This was investigated live on this machine (Windows 11 Enterprise, build 26200, `fSingleSessionPerUser=1` default):

- **Two monitors ≠ two sessions.** A multi-monitor setup is **one** session spanning both screens — it shares a single input desktop, foreground window, and `SendInput` queue across the monitors. Monitor count has nothing to do with session count, so "I have two monitors" does not give you two sessions to run two modules in.
- **Sessions are isolated** — each Windows session has its own input desktop, its own foreground window, and its own `SendInput` queue. So *typing in session B genuinely does NOT disturb session A's foreground or input.* Cross-session interference is **not** the problem (so if you somehow DID have two live sessions — Server/RDS — they could run in parallel without colliding).
- **The real blocker is session takeover.** Client Windows allows only **one interactive (console/owning) session at a time**, and `fSingleSessionPerUser=1` (the default) means one user gets **one** session. When you open the *second* RDP connection (as the same user), Windows **disconnects the first session** — it flips to `Disconnected`, its input desktop detaches, `GetForegroundWindow()` → 0, and any in-flight UI test there fails with `ACCESS_DENIED` → BLK-ENV. It's not your *typing* that breaks the test; it's the act of logging in the second session that evicts the first.
- A different *user* account doesn't rescue it either: client Windows still permits only one connected interactive session, so the second login still disconnects the first.
- Therefore, on client Windows, **run modules serially in one session.** True concurrent multi-session needs Windows Server + the RDS (Remote Desktop Session Host) role; unofficial multi-session patches exist but are out of scope here.

> **Verdict on the common assumption "I can run two modules in two RDP sessions because I have two monitors":** the *conclusion* (can't run two at once on client Windows) is correct, but the *reasoning* is wrong on two counts — two monitors is still one session, and you can't get two simultaneously-Active sessions on client Windows at all (the 2nd login disconnects the 1st). The limit is "can't open a 2nd Active session", not "the two sessions fight each other".

**Practical guidance:** keep a single RDP session for the whole run; don't reconnect/relogin mid-run; if you must check something elsewhere, alt-tab inside the *same* session rather than opening a new RDP connection. To detect a takeover after the fact, `qwinsta` will show your former session as `Disconnected`.

## Pre-run setup checklist

Run these BEFORE starting the verification agent.

### On the test machine (the one being verified)

```powershell
# Snapshot current power settings so you can restore after
$bk = "$env:TEMP\powercfg-backup-$(Get-Date -f yyyyMMdd-HHmmss).txt"
powercfg /query SCHEME_CURRENT SUB_SLEEP    > $bk
powercfg /query SCHEME_CURRENT SUB_VIDEO   >> $bk
"# Restore later with the values from $bk" | Set-Content "$bk.note"

# Disable sleep + display-off + hibernate (AC and battery)
powercfg /change standby-timeout-ac 0
powercfg /change standby-timeout-dc 0
powercfg /change monitor-timeout-ac 0
powercfg /change monitor-timeout-dc 0
powercfg /change hibernate-timeout-ac 0
powercfg /change hibernate-timeout-dc 0

# Disable screensaver
Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name ScreenSaveActive  -Value '0'
Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name ScreenSaveTimeOut -Value '0'

# Disable workstation lock-on-idle (requires admin)
# 0 = never lock. Restore your original value (commonly 600 = 10 min) afterward.
$origLock = (Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Policies\System' -Name InactivityTimeoutSecs -EA SilentlyContinue).InactivityTimeoutSecs
"$origLock" | Out-File "$bk.lock"
Set-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Policies\System' -Name InactivityTimeoutSecs -Value 0 -EA SilentlyContinue

# Confirm
powercfg /query SCHEME_CURRENT SUB_SLEEP | Select-String 'Power Setting GUID|Current AC Power Setting Index'
```

### On the local machine (the one with the RDP client)

```powershell
# Disable local sleep so RDP TCP stays alive
powercfg /change standby-timeout-ac 0

# Practical habit: put mstsc on a monitor you're NOT actively working on.
# Don't minimize. Alt-tab is fine; minimize is not.
```

## Mid-run discipline

While the agent is running:
- **Don't minimize mstsc.** Visible-but-unfocused is OK; minimized is not.
- **Don't close mstsc with the X.** If you have to step away, fine — leave it open.
- **Don't disconnect or reconnect RDP.** Stay continuously connected for the duration of the run.
- **Don't sign out** on either end.
- If you do step away and the screen locks (despite the setup above), reconnect/unlock and the agent's `Test-PtSessionStillInteractive` guard (if used) will resume; otherwise items mid-execution will be BLK-ENV.

## Post-run cleanup (restore)

```powershell
# Restore the values you captured to $bk before starting
# (e.g. typical defaults: standby 30min, monitor 15min, screensaver 600s, lock 600s)
powercfg /change standby-timeout-ac 30
powercfg /change standby-timeout-dc 15
powercfg /change monitor-timeout-ac 15
powercfg /change monitor-timeout-dc 10
powercfg /change hibernate-timeout-ac 0  # often default

Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name ScreenSaveActive  -Value '1'
Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name ScreenSaveTimeOut -Value '600'

$origLock = Get-Content "$bk.lock" -EA SilentlyContinue
if ($origLock) {
    Set-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Policies\System' `
        -Name InactivityTimeoutSecs -Value ([int]$origLock) -EA SilentlyContinue
}
```

(Values above are typical; adjust to your environment policy.)

## Diagnostic before you start

Run `scripts/pt-session-diagnose.ps1` from the agent shell. Expected output for a GO:

```
PASS - this shell can drive interactive PowerToys tests.
```

If it prints FAIL with a `psexec -i <consoleSession> -s pwsh.exe` hint, you're in a non-console session — relaunch the agent shell as suggested before starting verification.

## Why this isn't in the global SKILL.md

These are **human prep steps**, not agent instructions. The agent needs to *detect* a bad environment (via `Test-PtInteractiveDesktop` in pre-flight + `Test-PtSessionStillInteractive` mid-run); the user needs to *prevent* one. Different audiences, different docs.

## Related

- `scripts/pt-session-diagnose.ps1` — one-shot session diagnostic
- `scripts/pt-foreground-guard.ps1` — `Test-PtForeground` / `Force-PtForeground` / `Assert-PtForegroundOrAbort` used by agent
- `SKILL.md` pitfall #13 — short pointer to this doc
- `references/pre-flight.md` pre-flight check #4 — agent reads this doc when it detects a bad env
