# Window Hopper (Alt Window Cycle)

The native `AltWindowCycle` module runs inside the PowerToys Runner. A dedicated UI thread owns the
non-activating switcher windows; there is no separate module executable. Settings displays the module
as **Window Hopper**, while its settings directory and enabled-map key remain `AltWindowCycle`.

## UI tests

`src\modules\AltWindowCycle\Tests\AltWindowCycle.UITests` uses `Microsoft.PowerToys.UITest.Next`.
Run it non-elevated, with a live desktop and the repository-pinned winappcli:

```powershell
tools\build\build.cmd -Path src\modules\AltWindowCycle\Tests\AltWindowCycle.UITests -Platform x64 -Configuration Debug
& .\x64\Debug\tests\AltWindowCycle.UITests\net10.0-windows10.0.26100.0\AltWindowCycle.UITests.exe --report-trx --results-directory .\TestResults\AltWindowCycle
```

Build before running; use the local-VM skill to execute off the host. The same suite runs on Windows
10, Windows 11, and ARM64 CI. The pipeline dispatches it non-elevated and uses the existing
Runner/Settings companion signing setup for Release Settings IPC.

The tests cover [#51548](https://github.com/microsoft/PowerToys/issues/51548):

| Scenario | Automated coverage |
|---|---|
| Same-application filtering | Three separate processes with the same executable image, plus an interleaved different-image application; composed preview colors and exact activation targets |
| Forward/reverse cycling | One through four presses while holding Alt, including wraparound; no activation before modifier release |
| Direction changes | Shift can reverse the current cycle; releasing Shift alone does not commit |
| Customized shortcuts | Both shortcuts changed through Settings; Ctrl release commits the new chords; old chords stop working without a Runner restart |
| Disabled module | Real Settings toggle destroys module HWNDs; neither shortcut shows the switcher or changes foreground; re-enable restores cycling |
| Cancellation/single window | Escape preserves foreground and allows another cycle; an application with one window cannot switch to a different application |

The fixture is a small WinForms application, not Notepad or an installed app with version-dependent
tabs. Each source has a distinct solid color. A copied fixture at another executable path supplies the
different-application case because the product groups windows by full executable image path. The copy
is staged once per test class and removed at class cleanup; windows and processes remain isolated per test.

The GDI-painted switcher has no UIA card tree. Tests therefore inspect composed desktop pixels for
preview membership and assert actual foreground HWNDs for ordering and activation, rather than
adding product-only automation hooks or asserting private selection state. WinEvent show observers
catch even a briefly displayed switcher in negative checks. Tests restore exact module-settings
bytes (or prior absence), release held modifiers, and stop their owned fixture processes. Pre-test
hygiene also removes abandoned `AltWindowCycle.TestApp` processes left by an aborted prior run.
