# Display Profiles

Display Profiles stores settings, display profiles, diagnostics, and topology recovery data under
`%LOCALAPPDATA%\MonitorPower`.

The PowerToys runner starts `PowerToys.MonitorPower.Runtime.exe`, a single-instance WinUI host that
owns the global keyboard and XInput listeners independently of Command Palette. The Settings page
also starts the host when launched outside the runner. The host reloads the activation shortcut
and controller chord from `settings.json`, and opens the profile selector on activation. The
selector supports keyboard and controller navigation, explicit apply/cancel actions, and refuses
to apply a profile if the active display topology changed after it opened.

Runtime startup, listener registration, selector activation, and profile-apply timings/errors are
appended to `%LOCALAPPDATA%\MonitorPower\Logs\runtime.log`. Display-operation diagnostics are in
`%LOCALAPPDATA%\MonitorPower\diagnostics.log`; Settings initialization and navigation errors are
in `%LOCALAPPDATA%\Microsoft\PowerToys\Settings\Logs`.
