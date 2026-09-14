# ZoomIt virtual mirror experiment

This separate branch experiments with using a temporary virtual display as the
output of ZoomIt DemoMirror. With one physical monitor, select a region and let
DemoMirror reproduce it on a new display that a meeting application may offer for
screen sharing. This is a local experiment, not a released PowerToys feature.

The virtual display lifecycle is adapted from the separate RegionMirror PoC. The
existing DemoMirror capture and rendering path supplies the mirrored image,
including its zoom and drawing integration. There is no second RegionMirror
capture process. The experiment does not change the PowerToys installer, install
a driver automatically, or elevate ZoomIt automatically.

## Controls

The following are the default mirror shortcuts. If the mirror activation shortcut
is customized, use its corresponding Shift and Alt variants.

| Shortcut | Behavior in this branch |
| --- | --- |
| **Ctrl+Shift+9** | Select a screen region, then create a temporary virtual display and start DemoMirror on that display. |
| **Ctrl+9** | Mirror the screen to an existing second display, using the existing target selection behavior. |
| **Ctrl+Alt+9** | Mirror a specific window to an existing second display, using the existing target selection behavior. |

Press **Escape** to cancel region selection before creating a display. Press a
mirror shortcut again while creation is pending to cancel the operation. Press a
mirror shortcut while mirroring is active to stop. Exiting ZoomIt also stops the
session. Shutdown stops capture and closes the output window before releasing the
owned virtual display; Windows removes that display asynchronously.

The driver supplies supported display modes. The experiment requests 1920 x 1080
and chooses an available mode, and DemoMirror scales the image while preserving its aspect
ratio; an unmatched aspect ratio leaves unused space instead of stretching the
image. The new display is the explicit output target even if other monitors are
already connected.

Selection currently stays within one source monitor, matching ZoomIt's existing
region selector. Cross-monitor composition and RegionMirror's window-placement
management are not included in this experiment.

## External driver prerequisite

The temporary display path currently requires **Windows x64**, the pinned driver
package already staged in the Windows driver store, and the experimental x64
ZoomIt build launched **as administrator**. Other mirror modes continue to use
existing displays and do not create a virtual display.

- Source: [VirtualDrivers/Virtual-Display-Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver)
- Release: [25.7.23](https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/tag/25.7.23)
- License: [MIT](https://github.com/VirtualDrivers/Virtual-Display-Driver/blob/25.7.23/LICENSE)
- Asset: `VirtualDisplayDriver-x86.Driver.Only.zip`. Despite its name, the INF in this pinned package targets `NTamd64`.
- SHA-256: `e24210692b442b39af763536330ce78b423f19342b7a7792c26de3944e418b3a`
- Catalog and binary signer: SignPath Foundation. This does not imply WHQL certification.

No driver source or binaries are committed here or bundled with PowerToys. The
preparation script verifies the pinned archive hash, catalog and binary signatures,
and the expected x64 INF hardware ID. It leaves Windows trust and security settings
unchanged and does not launch the upstream Virtual Driver Control application.

Run this from the repository root to download and verify the package only:

```powershell
.\src\modules\ZoomIt\scripts\Prepare-VirtualDisplayDriver.ps1
```

If staging is needed, explicitly run the following from an administrator PowerShell
session:

```powershell
.\src\modules\ZoomIt\scripts\Prepare-VirtualDisplayDriver.ps1 -Stage
```

Only `-Stage` invokes `pnputil /add-driver`. It does not use `/install` or create a
persistent root display device. Staging does not add certificates, enable test
signing, or change Secure Boot. The package is stored beneath
`src/modules/ZoomIt/artifacts/virtual-display-driver/25.7.23`, which is ignored by
Git.

The current lifecycle implementation requires the driver's built-in one-monitor
defaults. It rejects an existing Virtual Display Driver device, a `VDDPATH`
registry override, or shared driver configuration files. It does not modify a
device or configuration owned by another application.

## Try the experiment

1. Prepare and explicitly stage the driver if it is not already available.
2. Close other ZoomIt instances (or disable ZoomIt in PowerToys) to avoid shortcut conflicts, then launch this branch's experimental x64 ZoomIt build as administrator: `src\modules\ZoomIt\ZoomIt\x64\Debug\PowerToys.ZoomIt.exe`.
3. Press **Ctrl+Shift+9**, select a region, and wait for the temporary display to appear. No second physical monitor is required.
4. Open the meeting application's screen-sharing picker and check whether it lists the new display. If available, share it and confirm the remote participant sees the selected region, cursor, and expected ZoomIt annotations.
5. Press a mirror shortcut again or exit ZoomIt. Confirm the mirrored output closes and the temporary display disappears.

Meeting software compatibility is **not established**. A display appearing in
Windows does not prove that a particular meeting client can list it, capture it,
or preserve zoom and drawing updates. Verify the remote participant's view before
using this experiment in a presentation. The selected region contains visible
screen content, including any other windows or notifications that appear there.

## Manual acceptance checklist

An initial interactive trial received positive user feedback on 2026-09-14.
The detailed runtime cases below are **pending** individual verification;
build and unit-test results are recorded separately below. Run the applicable cases
with one physical monitor, then repeat display-sensitive cases with multiple
monitors and different DPI settings.

- [ ] **Successful start:** With the pinned driver staged and ZoomIt elevated, select a region using Ctrl+Shift+9. Exactly one temporary display appears, DemoMirror uses it, and the mirrored image preserves the selection's aspect ratio. Zoom, drawing, and cursor updates behave as expected.
- [ ] **Selection cancellation:** Press Escape before and during selection. No temporary display or mirror output is created, and the next selection works.
- [ ] **Cancellation during creation:** Press a mirror shortcut again while the display is being created. No delayed mirror starts after cancellation, and any newly created device is removed.
- [ ] **Repeated start:** Complete several start/stop cycles, including a restart immediately after stopping. Each new session owns one temporary display; no stale output, device, or worker accumulates.
- [ ] **Creation failure:** Attempt virtual mirroring without elevation or without a usable staged driver. Confirm a clear failure, no automatic installation or elevation, no leftover output or device, and successful recovery after correcting the prerequisite. Also verify rejection of an existing VDD device or shared configuration without modifying it.
- [ ] **Stop:** Stop active virtual mirroring with a mirror shortcut. Capture and output close before the temporary display is removed; existing physical displays remain available and keep their layout.
- [ ] **Exit:** Exit ZoomIt while virtual mirroring is active and while creation is pending. No capture, output window, temporary display, or delayed start remains.
- [ ] **Display changes:** Change resolution, DPI, monitor arrangement, or disconnect a relevant display during creation and active mirroring. Confirm safe handling, no stale capture or invalid output target, and successful cleanup and restart after the layout settles.
- [ ] **Screen mirror regression:** With an existing second display, Ctrl+9 still uses the existing target selection behavior, creates no virtual device, and stops normally. With one physical display, it retains the existing second-monitor requirement.
- [ ] **Window mirror regression:** With an existing second display, Ctrl+Alt+9 still mirrors the chosen window to an existing target, creates no virtual device, and stops normally. With one physical display, it retains the existing second-monitor requirement.
- [ ] **Meeting-client sharing:** Record the client and version, confirm the virtual display is selectable, and verify the remote image, aspect ratio, cursor, zoom, drawing, and stopping behavior. Leave unsupported clients documented as unsupported.

## Build and unit-test validation

Final acceptance on 2026-09-14 used the repository build wrapper, Debug x64:

```powershell
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\ZoomIt\ZoomItBreak
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\ZoomIt\ZoomIt
.\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path .\src\modules\ZoomIt\unittests
```

All three projects finished with exit code 0. The final ZoomIt application and
test builds reported no warnings or errors. VS `vstest.console.exe` ran
`x64\Debug\tests\ZoomIt\ZoomItAnimation.UnitTests.dll`: **26 passed, 0 failed**,
including 10 new virtual-mirror layout tests. These cover negative coordinates,
invalid regions, source identity/placement changes, GDI renumbering, and target
overlap/replacement. They do not create a real display device.

The local test report is
`src/modules/ZoomIt/artifacts/validation/zoomit-virtual-mirror.trx`.
Detailed device-lifecycle checks and meeting-client compatibility remain **pending**.
