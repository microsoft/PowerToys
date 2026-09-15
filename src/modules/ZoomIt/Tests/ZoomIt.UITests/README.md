# ZoomIt UI tests

This greenfield suite uses `Microsoft.PowerToys.UITest.Next` and winappcli to automate
the [ZoomIt release checklist](https://github.com/microsoft/PowerToys/issues/40684).
It launches ZoomIt through the PowerToys runner, not as the standalone Sysinternals app.

## Coverage

| Checklist | Automated scenario and observable result |
|---|---|
| Enable ZoomIt | Settings off/on changes the actual process lifecycle; the configured shortcut then produces a magnified image. |
| 1 | Left- and right-clicking the real notification icon each opens exactly Break Timer, Draw, Zoom, and Record. |
| 2-3 | The Settings switch removes and restores the Shell icon, including the notification overflow. |
| 4 | Static zoom doubles a known marker, freezes subsequent source updates, and exits with both Escape and the configured shortcut. |
| 5 | Live Zoom magnifies the marker and displays subsequent source updates; its shortcut exits. |
| 6 | Draw preserves the original scale, renders a real mouse stroke, and exits with Escape. |
| 7, 14 | Native file selection supplies Demo Type; empty, indented, and populated Notepad lines receive the exact text without changing their existing prefix. A controlled native editor isolates the minimum/maximum typing-rate measurement from Notepad's formatting and document-loading work; faster typing reduces duration, and Escape stops a longer script. |
| 8 | Break mode renders centered timer digits and exits with Escape. |
| 9, 23 | Both the recording shortcut and the tray command capture real frames. Stopping opens the native save dialog; the saved MP4 has correct dimensions, duration, frame rate, and two decoded frames matching the source. |
| 10 | Snipping copies the exact selected rectangle. Every clipboard bitmap pixel and its dimensions are compared with the source, replacing the manual Paint inspection. |
| 11 | Both animation settings are delivered through Settings and preserve the final magnification and exit behavior. Animation smoothness remains a visual check. |
| 12 | The initial zoom slider changes the rendered marker to 1.25x and 4x. |
| 13 | The native font dialog changes the persisted font and the actual timer glyph geometry. |
| 15 | Timer opacity changes both the layered-window alpha and the composed background pixels. |
| 16 | Top-left and bottom-right position selections move the rendered timer; the default centered case is covered separately. |
| 17 | A selected bitmap appears across the timer background. |
| 18 | Sound enablement and file selection persist; a real one-minute countdown produces the additional overtime line. Audible playback still needs an audio-output-equipped machine and is not claimed by this test. |
| 19 | The opened microphone dropdown matches Windows AudioCapture enumeration, including the Default-only case on machines without microphones. |
| 20-22 | Real tray-menu clicks activate Break, Draw, and Zoom, with the same rendered-output assertions as shortcut activation. |

## Execution

Run in an isolated, English-language interactive desktop with winappcli, .NET 10, and
a PowerToys runtime. The executable embeds a PerMonitorV2 manifest. Pipeline-like runs
normalize the desktop to 1920x1080. Recording requires a working Windows Graphics Capture
display; lack of captured frames is an explicit failure, not a skipped or passing recording.
Suppress unrelated desktop notifications in the test environment before running: Shell
toasts can cover pixel samples even when ZoomIt's window is topmost.

```powershell
dotnet restore src\modules\ZoomIt\Tests\ZoomIt.UITests\ZoomIt.UITests.csproj -p:Platform=x64
tools\build\build.cmd -Path src\modules\ZoomIt\Tests\ZoomIt.UITests -Platform x64 -Configuration Debug

# Execute only on the test desktop, normally through ui-tests-local-vm.
.\x64\Debug\tests\ZoomIt.UITests\net10.0-windows10.0.26100.0\ZoomIt.UITests.exe --report-trx
```

The suite snapshots ZoomIt's registry values and uses deterministic initial settings.
Settings changes under test use the actual UI and interop/IPC path, without restarting
the runner to apply them. A once-per-class Shell restart clears stale notifications from
earlier interrupted runs. Teardown hides the tray icon before stopping ZoomIt, restores
registry values, and closes test-owned windows and files.
The Notepad fixture temporarily disables and then restores its spelling options:
recipient-side rewriting and spell-check processing must not be confused with the literal
text emitted by Demo Type. It also verifies the caret is after the existing prefix before
activation; the exact output assertion still requires that prefix to remain in place.

Successful screenshots, clipboard images, recordings, decoded frames, and microphone
inventories are stored beside MSTest's deployment directory so they survive successful-run
cleanup. Failure media is captured before teardown.

For delivery, run the complete suite on Windows 10 and Windows 11 with both default and
constrained VM profiles, then validate `[ZoomIt.UITests]` in UI Test Automation CI.
The pipeline's existing authenticated Settings IPC companion-signing selection includes
this project.
