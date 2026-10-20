# Runner - module verification profile

**PT module**: Runner (`PowerToys.exe`)
**Source**: `src\runner\`
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`
**Installed exe**: `%LOCALAPPDATA%\PowerToys\PowerToys.exe` or the machine-wide installation
**Tray window class**: `PToyTrayIconWindow`
**Last verified**: local x64 Release Runner shutdown fix based on main `5c9fddcddd`, with copied 0.102.2883.0 dependency payload, 2026-10-20

## Entry-paths

### 1. Notification-area menu

Discover the primary `Shell_TrayWnd` window. Inspect its notification area; if
needed, invoke the **Show Hidden Icons** button and inspect the overflow window.
Distinguish the Runner's versioned `PowerToys v...` tooltip from **PowerToys Awake**
and the Settings taskbar application button. Discover the current selector from
the fresh UIA tree rather than reusing a slug from an earlier launch.

Right-click the Runner icon with `winapp ui click --right`. Its actual Win32 menu
uses **Close** in English, not **Exit**. Inspect the menu owned by the recorded
Runner PID and use its current selector; command ID `40001` is
`ID_CLOSE_MENU_COMMAND` in `tray_icon.cpp`.

### 2. Targeted tray notification

If the Explorer icon cannot be focused reliably, locate the recorded PID's
`PToyTrayIconWindow` with Win32 enumeration and post `WM_APP` with
`lParam=WM_CONTEXTMENU`. This opens the same tray menu; then use `winapp ui` to
inspect, capture and activate its Close item. Record that the opener was a
targeted notification rather than a physical right-click.

### 3. Process-level graceful-close regression

`tools\build\Test-RunnerShutdown.ps1` starts a supplied complete test payload,
posts `WM_CLOSE`, and records the exact process's natural termination. It refuses
an existing Runner in the session. A timeout/crash fails the check, even if failed
fixture cleanup subsequently terminates that process.

## Recipes

| Capability | Control |
|---|---|
| Tray Quit | Versioned Runner notification icon, then the real Close menu item |
| Repeated startup/exit | `Test-RunnerShutdown.ps1 -RunnerPath <payload>\PowerToys.exe` |
| Fast exit after startup | The regression script's `-SettleSeconds 0` |
| Single-instance guard during cleanup | Launch a second copy while the first PID is still in recorded module cleanup |
| Settings-requested restart | General language selection, then the language-change Restart button |
| Restart Manager close protocol | `WM_QUERYENDSESSION` / `WM_ENDSESSION` with `ENDSESSION_CLOSEAPP` to the owned test window |

**Read-out notes**

- Check the specific PID/handle and exit code. A disappeared tray icon or window
  does not prove that `PowerToys.exe` exited.
- Prove the tested Runner by executable path and hash. An unchanged Settings
  dependency can display its own release version; that is not Runner provenance.
- Look for the PID's `Runner WM_DESTROY`, pre-CRT module-unload, and completed
  shutdown log entries. On a timeout, preserve stacks before cleaning up a
  recorded failed fixture; never present forced cleanup as a pass.
- A copied dependency layout with locally built native components is scoped
  component verification, not full-main or installer sign-off. Keep the actual
  installation immutable and verify interface compatibility before staging.

## BLOCKED traps

- WinUI Settings may have a process before its visible main window exists. Wait
  for that window, then wait for navigation and scroll the language Restart
  button into view.
- A reported `isInvokable` value does not guarantee the current provider offers
  InvokePattern. Record failures and use the documented click fallback only when
  an interactive input desktop is available.
- A locked or detached RDP desktop can still expose cached trees/screenshots
  while rejecting current patterns and cursor access. Do not count an attempted
  restart as successful: require the old PID to terminate and identify its real
  replacement.
- ZoomIt hotkey-conflict handling can activate another Runner with
  `--open-settings=ZoomIt`. Record that external activation separately from a
  shutdown hang. If a controlled profile disables ZoomIt, disclose the reduced
  scope and restore the original setting.
- Genuine OS shutdown/logoff is not equivalent to posting Restart Manager close
  messages. Use a disposable environment for the former and label the latter as
  protocol coverage.
- Runner startup can recreate the current user's `\PowerToys\` autorun task with
  the test executable path even when the startup preference was already enabled.
  Snapshot the task definition/security before testing and restore the installed
  executable action afterward. Restoring `settings.json` alone does not restore it.

## Fixtures

- Separate baseline and fixed payload directories, with executable/PDB hashes.
- Private configuration snapshots; do not upload user settings or raw backups.
- Current-user autorun task definition, principal and security descriptor.
- PID-scoped cleanup records, including any pre-existing exited remnants.
- Restore only the input state a test changed; do not attempt to alter a locked
  desktop after an accessibility-only test that did not move the pointer.

## Source citations

- `src\runner\tray_icon.cpp`: `handle_tray_command`, `tray_icon_window_proc`, `start_tray_icon`.
- `src\runner\main.cpp`: `WinMain` module shutdown guard and lifetime locks.
- `src\runner\general_settings.cpp`: `apply_general_settings`; `auto_start_helper.cpp`: `create_auto_start_task_for_this_user`.
- `src\runner\powertoy_module.cpp`: `modules`.
- `src\settings-ui\Settings.UI\SettingsXAML\Views\GeneralPage.xaml.cs`: `Click_LanguageRestart`.
- `src\modules\ZoomIt\ZoomIt\Zoomit.cpp`: `OpenPowerToysSettingsApp` and hotkey-conflict handling.
