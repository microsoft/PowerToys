# Region Mirror — local proof of concept

An independent, directly executable module for issue [#2774](https://github.com/microsoft/PowerToys/issues/2774):

1. Select a rectangle on one physical screen.
2. Create a temporary virtual monitor.
3. Mirror the selected pixels onto that monitor, which a meeting application can share as a screen.

Application windows stay on the physical display. Changing applications inside the rectangle does not require selecting a new sharing source.

This experiment is **local only**. It is not registered with Runner or Settings, added to the main solution, packaged, signed, or enabled in release pipelines. It does not depend on FancyZones or ZoomIt.

See [local acceptance results](VALIDATION.md) for the tested scope and remaining validation.

## Prerequisites

- Windows x64 with a local interactive desktop; the initial target is Windows 11 with SDR displays.
- Visual Studio C++ tools, Windows SDK 10.0.26100.0, and the native C++ test tools.
- The separately downloaded, signed Virtual Display Driver package described below.
- An elevated application process for Windows SwDeviceCreate. The executable is asInvoker; it does not automatically elevate or modify Windows security settings.

The native projects use Windows SDK libraries and C++/WinRT headers only. The module-local MSBuild properties isolate the experiment from the repository's shipping dependencies.

## Final acceptance build

From the repository root, run each project through the repository's build wrapper:

~~~powershell
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\RegionMirror\RegionMirror
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\RegionMirror\RegionMirrorTests
.\tools\build\build.ps1 -Platform x64 -Configuration Release -Path .\src\modules\RegionMirror\RegionMirrorFuzzTests
~~~

Run these only for final acceptance or when a build is explicitly requested. All three commands must exit with code 0 before running their outputs. No NuGet restore or full PowerToys build is needed.

The application is produced at:

~~~text
src\modules\RegionMirror\bin\x64\Debug\PowerToys.RegionMirror.exe
~~~

## Prepare the external driver

~~~powershell
.\src\modules\RegionMirror\scripts\Prepare-VirtualDisplayDriver.ps1
~~~

This downloads **VirtualDrivers/Virtual-Display-Driver 25.7.23**, verifies a pinned SHA-256 and Windows Authenticode signatures, and extracts the package under the ignored module artifacts directory. It does not install anything.

Once ready, run the same script **from an administrator PowerShell** with the Stage switch:

~~~powershell
.\src\modules\RegionMirror\scripts\Prepare-VirtualDisplayDriver.ps1 -Stage
~~~

This stages the signed INF with pnputil /add-driver; it does not install a persistent root display device. The preparation script never adds a certificate, enables test signing, or changes Secure Boot.

External development prerequisite:

- Source / MIT license: https://github.com/VirtualDrivers/Virtual-Display-Driver
- Pinned release: https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/tag/25.7.23
- Asset: VirtualDisplayDriver-x86.Driver.Only.zip (the INF inside targets **NTamd64**, despite the archive name).
- SHA-256: e24210692b442b39af763536330ce78b423f19342b7a7792c26de3944e418b3a
- Catalog and binary signer: SignPath Foundation. This is not a claim of WHQL certification.
- No third-party driver source or binaries are committed or included in the PowerToys distribution.

## Run

Launch PowerToys.RegionMirror.exe using **Run as administrator**:

1. Click **Select region**, drag within a single source screen, then release. Escape, right-click, or losing focus cancels; dragging beyond the initial monitor clamps to its edges.
2. Click **Start mirror**. A new virtual display appears to the right of the current desktop. The driver supplies supported modes; the closest available mode is chosen. Other aspect ratios are letterboxed, without stretching.
3. In the meeting application, choose the new screen. The controller shows its Windows display name and the number of frames presented.
4. Click **Stop**, use **Ctrl+Alt+Q**, or close the controller. Capture and output windows stop before the app releases its software-device handle, initiating removal of its virtual monitor.

The source is a rectangle of visible desktop pixels. Notifications and other applications appearing inside it are included. The controller and green region indicator are excluded from capture; the output is deliberately capturable. The system capture border remains enabled.

The app owns only the device it creates. It refuses to start if an existing MttVDD device or shared VDD configuration override is present, rather than changing another tool's virtual displays or settings. It never uses the driver's reload/count pipe. Stopping the app removes its transient device; the staged driver package remains in Windows' driver store.

## Diagnostics and repeatable capture checks

List active displays without installing or creating a device (all commands below are single lines):

~~~powershell
& .\src\modules\RegionMirror\bin\x64\Debug\PowerToys.RegionMirror.exe --list-displays --report "$env:TEMP\region-mirror-displays.json"
~~~

Supply a rectangle in physical desktop pixels and an automatic stop time:

~~~powershell
& .\src\modules\RegionMirror\bin\x64\Debug\PowerToys.RegionMirror.exe --region "100,100,960,540" --duration 10 --report "$env:TEMP\region-mirror-result.json"
~~~

Device creation still requires elevation. The report records the owned device instance, target display, frames presented, error, and displays observed after releasing the device. PnP removal is asynchronous; verify the device has disappeared separately before asserting complete cleanup.

For capture-only diagnostics, the explicit option --target-display "\\.\DISPLAY2" uses an **existing** display instead of creating one. This never changes that display's mode or removes it, but temporarily covers it with the mirror output. It cannot target the source monitor. This diagnostic does not validate virtual-device creation.

Run RegionMirrorTests.dll through Visual Studio Test Explorer or vstest.console.exe (not dotnet test). See [fuzzing instructions](RegionMirrorFuzzTests/README.md) and [manual acceptance](RegionMirrorTests/ManualAcceptance.md).

The complete bounded integration check is available in scripts/Validate-VirtualDisplay.ps1. Run it elevated after building; add -StageDriver only when the external package also needs staging. It captures for 15 seconds, records frame counts, and waits for removal and restoration of the original physical layout using stable monitor identities. It does not join or share a meeting.

## Architecture and current limits

- SelectionOverlay: per-monitor-aware Win32 selection in physical coordinates.
- VirtualDisplay: software device with the installed MttVDD hardware ID; verifies PnP ownership, chooses an advertised display mode, and applies a transient position without CDS_UPDATEREGISTRY.
- CaptureMirror: Windows Graphics Capture on an MTA worker, D3D11 texture crop and flip-model swapchain. Source geometry changes or capture errors end the session.
- Main: independent controller, aspect-fitted output, cancellation, stop hotkey, diagnostic reports.
- Geometry: checked integer geometry and strict CLI rectangle parsing, covered by native unit tests and a libFuzzer target.

The initial PoC does not promise HDR color accuracy, remote-session support, cross-monitor selections, arbitrary virtual resolutions, or compatibility with every meeting client. The controller uses its initial DPI for child-control sizing. A virtual monitor is an actual extended desktop: the mouse and unrelated windows can enter it. Input redirection and cursor confinement are not implemented.

The creation → capture → removal path passed local acceptance. Meeting sharing must still be checked in the intended client; no Teams sharing is started automatically.
