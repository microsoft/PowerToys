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

Run through Visual Studio Test Explorer or the test executable on an English-language
interactive host, or through the local VM/CI workflow. A quiet, isolated test desktop or
disposable VM is recommended. The suite takes foreground, minimizes windows, restarts
Explorer and PowerToys, and temporarily rewrites the real signed-in user's
`HKCU\Software\Sysinternals\ZoomIt` settings, shared with standalone Sysinternals ZoomIt.
Save work before starting, do not interact with the desktop during a run, and do not run
while using an unsaved ZoomIt drawing or recording. Settings are backed up and restored
as described below.

The suite requires winappcli, .NET 10, and a PowerToys runtime. The executable embeds a
PerMonitorV2 manifest. Host runs retain their display resolution and DPI; only pipeline-like
runs normalize the desktop to 1920x1080. Recording assertions account for the encoder's
even-pixel padding, and timer placement accounts for padded text and font-relative ink
bounds. Recording requires a working Windows Graphics Capture
display; lack of captured frames is an explicit failure, not a skipped or passing recording.
Suppress unrelated desktop notifications in the test environment before running: Shell
toasts can cover pixel samples even when ZoomIt's window is topmost.
Native dialogs are clicked once, then polled separately for window appearance and
HWND-scoped control readiness. A delayed Font dialog regression covers an eight-second
opening delay, beyond the previous five-second discovery timeout.

```powershell
dotnet restore src\modules\ZoomIt\Tests\ZoomIt.UITests\ZoomIt.UITests.csproj -p:Platform=x64
tools\build\build.cmd -Path src\modules\ZoomIt\Tests\ZoomIt.UITests -Platform x64 -Configuration Debug

# Run on the prepared host desktop, or use ui-tests-local-vm for isolation.
.\x64\Debug\tests\ZoomIt.UITests\net10.0-windows10.0.26100.0\ZoomIt.UITests.exe --report-trx
```

The suite writes a pre-mutation `zoomit-registry-original.json` backup under
`ZoomItEvidence` beside MSTest's deployment directory, then uses deterministic initial
settings. The backup records whether the key existed and each original value's name,
registry type, and data (binary data is base64). Preserve it for recovery if the test
process is terminated before cleanup; stop PowerToys/ZoomIt before restoring those values.
Pending snapshots are restored before a later test can capture test defaults, with a
class-cleanup safety net for initialization failures. Previously absent keys are removed
on restoration; existing empty keys are preserved.
Settings changes under test use the actual UI and interop/IPC path, without restarting
the runner to apply them. A once-per-class Shell restart clears stale notifications from
earlier interrupted runs. Teardown hides the tray icon before stopping ZoomIt, restores
registry values, and closes test-owned windows and files.
The Notepad fixture temporarily disables and then restores its spelling options:
recipient-side rewriting and spell-check processing must not be confused with the literal
text emitted by Demo Type. It also verifies the caret is after the existing prefix before
activation; the exact output assertion still requires that prefix to remain in place.
A calibrated, read-only input observer independently checks the Notepad probe fix:
all scripted Unicode key pairs must arrive, without extra virtual Space/Backspace events.
This distinguishes the old and fixed behavior even when both produce identical final text.

Fixture regressions cover abandoned registry initialization, recovery backups, original
key existence, source-window startup failure, and shortcut diagnostics. Buffered pixel
reads are compared with the former `GetPixel` oracle, including cropped and negative-stride
bitmaps; full-screen scan timings are recorded without imposing a machine-speed threshold.

Successful screenshots, clipboard images, recordings, decoded frames, and microphone
inventories are stored beside MSTest's deployment directory so they survive successful-run
cleanup. Failure media is captured before teardown.

For delivery, run the complete suite on Windows 10 and Windows 11 with both default and
constrained VM profiles, then validate `[ZoomIt.UITests]` in UI Test Automation CI.
The pipeline's existing authenticated Settings IPC companion-signing selection includes
this project.
