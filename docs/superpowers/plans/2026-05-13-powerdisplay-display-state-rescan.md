# PowerDisplay — Console-Display-State Rescan and VCP Write Retry: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover DDC/CI handles reliably after monitor-power transitions (sleep, idle blank, lid open/close, S0ix wake) without polling, and ensure user-initiated VCP writes don't fail silently on transient I²C errors.

**Architecture:** Replace `SystemEvents.PowerModeChanged` with `PowerSettingRegisterNotification(GUID_CONSOLE_DISPLAY_STATE)` via callback (no HWND coupling). When the console display transitions to ON, emit a new `ResumeDetected` event so `MainViewModel` can lock the UI **before** the existing debounce window — closing the race between wake and rescan. Independently, give `SetVcpFeatureAsync` a 3-attempt retry with 200ms backoff to absorb transient DDC/I²C failures that the rescan path cannot eliminate. The legacy `PowerModeChanged` subscription is removed entirely — `GUID_CONSOLE_DISPLAY_STATE` is a strict superset (fires on both S3 and S0ix, plus idle-blank scenarios that never trigger system sleep).

**Tech Stack:** C# (.NET 10, WinUI 3), P/Invoke to `powrprof.dll`, MSTest for unit coverage of pure state-machine logic. PowerToys PowerDisplay module (`src/modules/powerdisplay/`).

---

## File Structure

| Action | Path | Purpose |
|---|---|---|
| **Create** | `src/modules/powerdisplay/PowerDisplay/Helpers/PowerSettingsNative.cs` | P/Invoke definitions, GUID constants, and `POWERBROADCAST_SETTING` / `DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS` structs |
| **Modify** | `src/modules/powerdisplay/PowerDisplay/Helpers/DisplayChangeWatcher.cs` | Replace `SystemEvents.PowerModeChanged` with `PowerSettingRegisterNotification` callback; add `ResumeDetected` event; extract `ShouldTriggerOnDisplayState` as testable pure method |
| **Modify** | `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs` | Subscribe to new `ResumeDetected` event and set `IsScanning = true` immediately, closing the debounce-window race |
| **Modify** | `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs` | `SetVcpFeatureAsync` becomes a 3-attempt retry with 200ms inter-attempt delay |
| **Create** | `src/modules/powerdisplay/PowerDisplay.UnitTests/DisplayStateTransitionTests.cs` (new test project — see Task 0) | Unit-test the `ShouldTriggerOnDisplayState` pure function |
| **Create** | `src/modules/powerdisplay/PowerDisplay.UnitTests/PowerDisplay.UnitTests.csproj` | New MSTest project for testing main-app pure logic |
| **Modify** | `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md` | Add a "Sleep/wake + idle-blank recovery" section to the manual verification checklist |

---

## Task 0: Bootstrap PowerDisplay.UnitTests Test Project

**Why:** The main `PowerDisplay` project currently has no unit-test project (only `PowerDisplay.Lib.UnitTests` exists). To unit-test the pure state-transition logic we will extract in Task 2, we need a sibling test project that references `PowerDisplay`. Skipping this step would force us to skip unit-tests entirely or pollute `PowerDisplay.Lib.UnitTests` with cross-project references.

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.UnitTests/PowerDisplay.UnitTests.csproj`
- Create: `src/modules/powerdisplay/PowerDisplay.UnitTests/Usings.cs`

- [ ] **Step 1: Create the test project file**

Create `src/modules/powerdisplay/PowerDisplay.UnitTests/PowerDisplay.UnitTests.csproj` with this exact content (mirrored from `PowerDisplay.Lib.UnitTests.csproj` with project name adjusted):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <Import Project="..\..\..\..\Common.Dotnet.CsWinRT.props" />

  <PropertyGroup>
    <ProjectGuid>{B7A9C2D8-3E4F-4A5B-9C8D-1F2E3D4C5B6A}</ProjectGuid>
    <RootNamespace>PowerDisplay.UnitTests</RootNamespace>
    <AssemblyName>PowerDisplay.UnitTests</AssemblyName>
    <UseWinUI>true</UseWinUI>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Platforms>x64;ARM64</Platforms>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PowerDisplay\PowerDisplay.csproj" />
  </ItemGroup>

</Project>
```

Note: pin a fresh GUID. If the GUID above is already used elsewhere in the solution, generate a new v4 GUID and replace.

- [ ] **Step 2: Create a Usings.cs for test-project-wide imports**

Create `src/modules/powerdisplay/PowerDisplay.UnitTests/Usings.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

global using Microsoft.VisualStudio.TestTools.UnitTesting;
```

- [ ] **Step 3: Register the new project in the PowerToys solution**

From repo root, run:

```powershell
dotnet sln PowerToys.sln add src\modules\powerdisplay\PowerDisplay.UnitTests\PowerDisplay.UnitTests.csproj
```

This appends the standard `Project(...) = ...` block and a default `GlobalSection(ProjectConfigurationPlatforms)` mapping. Verify by running `git diff PowerToys.sln` — expect a clean addition of one project entry plus four configuration lines (Debug/Release × x64/ARM64).

If your editor / IDE expects a specific solution folder placement (PowerDisplay siblings sit under a "modules" solution folder), open `PowerToys.sln` after the command and manually move the project into that folder via Visual Studio's Solution Explorer drag-and-drop, then save.

- [ ] **Step 4: Verify project builds and discovers zero tests**

Run from repo root:
```powershell
dotnet build src\modules\powerdisplay\PowerDisplay.UnitTests\PowerDisplay.UnitTests.csproj -c Debug -p:Platform=x64
```
Expected: build succeeds, no tests yet.

- [ ] **Step 5: Commit**

```powershell
git add src/modules/powerdisplay/PowerDisplay.UnitTests/
# If solution was edited:
git add PowerToys.sln
git commit -m "test(PowerDisplay): bootstrap PowerDisplay.UnitTests project for pure-logic tests"
```

---

## Task 1: PowerSettingsNative — P/Invoke layer

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay/Helpers/PowerSettingsNative.cs`

- [ ] **Step 1: Create PowerSettingsNative.cs with full P/Invoke surface**

Create `src/modules/powerdisplay/PowerDisplay/Helpers/PowerSettingsNative.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Helpers;

/// <summary>
/// P/Invoke surface for the console-display-state power-setting notification.
/// Centralises the powrprof.dll bindings, GUIDs, and structures used by
/// <see cref="DisplayChangeWatcher"/>. No business logic lives here.
/// </summary>
internal static class PowerSettingsNative
{
    /// <summary>
    /// GUID_CONSOLE_DISPLAY_STATE — Windows fires this whenever the console
    /// display's power state changes. Data byte is 0 (off), 1 (on), 2 (dimmed).
    /// Reliable on both S3 and S0ix, and also fires for idle-blank / lid /
    /// screensaver transitions that never enter system sleep.
    /// </summary>
    public static readonly Guid GuidConsoleDisplayState =
        new("6fe69556-704a-47a0-8f24-c28d936fda47");

    public const uint DisplayStateOff = 0;
    public const uint DisplayStateOn = 1;
    public const uint DisplayStateDimmed = 2;

    /// <summary>
    /// DEVICE_NOTIFY_CALLBACK — Recipient is a pointer to
    /// DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS containing a callback delegate.
    /// </summary>
    public const uint DeviceNotifyCallback = 0x00000002;

    /// <summary>
    /// PBT_POWERSETTINGCHANGE — the Type parameter of the callback when a
    /// subscribed power setting changes.
    /// </summary>
    public const uint PbtPowerSettingChange = 0x8013;

    /// <summary>
    /// Callback signature for power-setting notifications.
    /// Windows invokes this on an internal worker thread — implementations
    /// must marshal to their own thread before touching UI / VM state.
    /// </summary>
    /// <param name="context">Opaque context supplied at registration.</param>
    /// <param name="type">Notification type (e.g. PBT_POWERSETTINGCHANGE).</param>
    /// <param name="setting">Pointer to a POWERBROADCAST_SETTING.</param>
    /// <returns>Reserved — must return 0.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate uint DeviceNotifyCallbackRoutine(IntPtr context, uint type, IntPtr setting);

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceNotifySubscribeParameters
    {
        public IntPtr Callback;
        public IntPtr Context;
    }

    /// <summary>
    /// POWERBROADCAST_SETTING — the payload Windows passes to the callback.
    /// PowerSetting (16 bytes) + DataLength (4 bytes) precede a variable-length
    /// Data array whose first byte holds the new display state.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PowerBroadcastSettingHeader
    {
        public Guid PowerSetting;
        public uint DataLength;
        // Data[] follows in memory at offset 20.
    }

    public const int PowerBroadcastSettingDataOffset = 20;

    [DllImport("powrprof.dll", SetLastError = false)]
    public static extern uint PowerSettingRegisterNotification(
        ref Guid settingGuid,
        uint flags,
        ref DeviceNotifySubscribeParameters recipient,
        out IntPtr registrationHandle);

    [DllImport("powrprof.dll", SetLastError = false)]
    public static extern uint PowerSettingUnregisterNotification(IntPtr registrationHandle);
}
```

- [ ] **Step 2: Build PowerDisplay project to verify the file compiles**

Run from repo root:
```powershell
dotnet build src\modules\powerdisplay\PowerDisplay\PowerDisplay.csproj -c Debug -p:Platform=x64
```
Expected: build succeeds. No warnings related to PowerSettingsNative.

- [ ] **Step 3: Commit**

```powershell
git add src/modules/powerdisplay/PowerDisplay/Helpers/PowerSettingsNative.cs
git commit -m "feat(PowerDisplay): add PowerSettingsNative P/Invoke for console display state"
```

---

## Task 2: DisplayChangeWatcher — Replace PowerModeChanged with console-display-state notification

This task is the heart of the change. We're doing four things atomically:

1. Extract a pure helper `ShouldTriggerOnDisplayState` (testable in isolation)
2. Delete `SystemEvents.PowerModeChanged` subscription and `OnPowerModeChanged` handler entirely
3. Wire up `PowerSettingRegisterNotification` with a managed callback delegate, protected from GC
4. Add a new `ResumeDetected` event that fires synchronously (on the dispatcher thread) **before** `ScheduleDisplayChanged` queues its debounce

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/DisplayChangeWatcher.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.UnitTests/DisplayStateTransitionTests.cs`

- [ ] **Step 1: Write the failing test for `ShouldTriggerOnDisplayState`**

Create `src/modules/powerdisplay/PowerDisplay.UnitTests/DisplayStateTransitionTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Helpers;

namespace PowerDisplay.UnitTests;

[TestClass]
public class DisplayStateTransitionTests
{
    [TestMethod]
    public void Trigger_WhenOffToOn_ReturnsTrue()
    {
        Assert.IsTrue(DisplayChangeWatcher.ShouldTriggerOnDisplayState(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateOff));
    }

    [TestMethod]
    public void Trigger_WhenDimmedToOn_ReturnsTrue()
    {
        Assert.IsTrue(DisplayChangeWatcher.ShouldTriggerOnDisplayState(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateDimmed));
    }

    [TestMethod]
    public void Trigger_WhenOnToOn_ReturnsFalse()
    {
        // Initial-state echo from the subscription, or a no-op event.
        Assert.IsFalse(DisplayChangeWatcher.ShouldTriggerOnDisplayState(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateOn));
    }

    [TestMethod]
    public void Trigger_WhenOnToOff_ReturnsFalse()
    {
        // We only rescan on wake (off → on), not on blank.
        Assert.IsFalse(DisplayChangeWatcher.ShouldTriggerOnDisplayState(
            newState: PowerSettingsNative.DisplayStateOff,
            lastState: PowerSettingsNative.DisplayStateOn));
    }

    [TestMethod]
    public void Trigger_WhenOnToDimmed_ReturnsFalse()
    {
        Assert.IsFalse(DisplayChangeWatcher.ShouldTriggerOnDisplayState(
            newState: PowerSettingsNative.DisplayStateDimmed,
            lastState: PowerSettingsNative.DisplayStateOn));
    }
}
```

- [ ] **Step 2: Run the new test to confirm it fails**

Run:
```powershell
dotnet test src\modules\powerdisplay\PowerDisplay.UnitTests\PowerDisplay.UnitTests.csproj -c Debug -p:Platform=x64
```
Expected: 5 tests fail with `CS0103: The name 'ShouldTriggerOnDisplayState' does not exist in the current context` or a similar compile error referring to the not-yet-added method.

- [ ] **Step 3: Rewrite DisplayChangeWatcher.cs end-to-end**

Replace the **entire** contents of `src/modules/powerdisplay/PowerDisplay/Helpers/DisplayChangeWatcher.cs` with:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;

namespace PowerDisplay.Helpers;

/// <summary>
/// Watches for display/monitor configuration changes and emits coalesced refresh
/// signals. Two independent sources feed in:
///
/// 1. <see cref="DeviceWatcher"/> over <c>DisplayMonitor</c> — picks up device
///    enumeration changes (plug, unplug, GPU rebind).
/// 2. <see cref="PowerSettingsNative.GuidConsoleDisplayState"/> — fires whenever
///    the console display transitions on/off/dimmed. Reliable on both S3 and
///    S0ix and the only signal that catches idle-blank or lid recovery.
///
/// On display turning ON, the watcher first fires <see cref="ResumeDetected"/>
/// synchronously (so the ViewModel can lock interactive UI immediately), then
/// schedules a debounced <see cref="DisplayChanged"/> so callers re-discover
/// hardware once it has stabilised.
/// </summary>
public sealed partial class DisplayChangeWatcher : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly TimeSpan _debounceDelay;

    private DeviceWatcher? _deviceWatcher;
    private CancellationTokenSource? _debounceCts;
    private bool _isRunning;
    private bool _disposed;
    private bool _initialEnumerationComplete;

    // Console-display-state subscription
    private IntPtr _displayStateRegistration;
    private PowerSettingsNative.DeviceNotifyCallbackRoutine? _displayStateCallback;
    private GCHandle _displayStateCallbackHandle;
    private uint _lastDisplayState = PowerSettingsNative.DisplayStateOn;

    /// <summary>
    /// Event triggered when display configuration changes (after debounce period).
    /// Subscribers (typically the ViewModel) should re-run monitor discovery.
    /// </summary>
    public event EventHandler? DisplayChanged;

    /// <summary>
    /// Event triggered the moment we detect the console display transitioning
    /// to ON. Fires on the dispatcher thread, synchronously, BEFORE the debounce
    /// delay elapses — gives subscribers a chance to lock interactive UI so
    /// users can't act on now-stale DDC/CI handles during the rediscovery
    /// window.
    /// </summary>
    public event EventHandler? ResumeDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayChangeWatcher"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">The dispatcher queue for UI thread marshalling.</param>
    /// <param name="debounceDelay">Delay before triggering DisplayChanged event. Allows hardware to stabilize after plug/unplug or wake.</param>
    public DisplayChangeWatcher(DispatcherQueue dispatcherQueue, TimeSpan debounceDelay)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _debounceDelay = debounceDelay;

        RegisterDisplayStateNotification();
    }

    /// <summary>
    /// Pure decision function — extracted so it can be unit-tested in isolation
    /// from the P/Invoke surface and dispatcher plumbing.
    /// Returns true if the watcher should treat this transition as a wake event
    /// and notify subscribers.
    /// </summary>
    internal static bool ShouldTriggerOnDisplayState(uint newState, uint lastState)
        => newState == PowerSettingsNative.DisplayStateOn
           && lastState != PowerSettingsNative.DisplayStateOn;

    public bool IsRunning => _isRunning;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
        {
            return;
        }

        try
        {
            string selector = DisplayMonitor.GetDeviceSelector();

            _deviceWatcher = DeviceInformation.CreateWatcher(selector);
            _deviceWatcher.Added += OnDeviceAdded;
            _deviceWatcher.Removed += OnDeviceRemoved;
            _deviceWatcher.Updated += OnDeviceUpdated;
            _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
            _deviceWatcher.Stopped += OnWatcherStopped;

            _initialEnumerationComplete = false;
            _isRunning = true;

            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Failed to start: {ex.Message}");
            _isRunning = false;
        }
    }

    public void Stop()
    {
        if (!_isRunning || _deviceWatcher == null)
        {
            return;
        }

        try
        {
            CancelDebounce();
            _deviceWatcher.Stop();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Error stopping watcher: {ex.Message}");
        }
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || !_initialEnumerationComplete)
            {
                return;
            }

            ScheduleDisplayChanged();
        });
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // Property-level updates fire frequently and are not actionable here.
        // Added/Removed are the primary device-level triggers.
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _initialEnumerationComplete = true;
        });
    }

    private void OnWatcherStopped(DeviceWatcher sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _isRunning = false;

            if (!_disposed)
            {
                Logger.LogInfo("[DisplayChangeWatcher] Watcher stopped unexpectedly, attempting restart");
                CleanupDeviceWatcher();

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (!_disposed && !_isRunning)
                        {
                            Start();
                        }
                    });
                });
            }
            else
            {
                _initialEnumerationComplete = false;
            }
        });
    }

    private void RegisterDisplayStateNotification()
    {
        _displayStateCallback = OnDisplayStateChangedNative;
        _displayStateCallbackHandle = GCHandle.Alloc(_displayStateCallback);

        var subscribeParams = new PowerSettingsNative.DeviceNotifySubscribeParameters
        {
            Callback = Marshal.GetFunctionPointerForDelegate(_displayStateCallback),
            Context = IntPtr.Zero,
        };

        var guid = PowerSettingsNative.GuidConsoleDisplayState;
        uint result = PowerSettingsNative.PowerSettingRegisterNotification(
            ref guid,
            PowerSettingsNative.DeviceNotifyCallback,
            ref subscribeParams,
            out _displayStateRegistration);

        if (result != 0)
        {
            Logger.LogWarning(
                $"[DisplayChangeWatcher] PowerSettingRegisterNotification failed: 0x{result:X}");
            _displayStateRegistration = IntPtr.Zero;
        }
        else
        {
            Logger.LogInfo("[DisplayChangeWatcher] Subscribed to GUID_CONSOLE_DISPLAY_STATE");
        }
    }

    /// <summary>
    /// Callback invoked by Windows on an internal worker thread when the
    /// console display power state changes. Reads the new state, marshals to
    /// the dispatcher, and lets <see cref="HandleDisplayStateChange"/> apply
    /// the policy.
    /// </summary>
    private uint OnDisplayStateChangedNative(IntPtr context, uint type, IntPtr setting)
    {
        if (type != PowerSettingsNative.PbtPowerSettingChange || setting == IntPtr.Zero)
        {
            return 0;
        }

        // Read the new state byte from POWERBROADCAST_SETTING.Data[0].
        uint newState;
        try
        {
            newState = Marshal.ReadByte(setting, PowerSettingsNative.PowerBroadcastSettingDataOffset);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DisplayChangeWatcher] Failed to read POWERBROADCAST_SETTING data: {ex.Message}");
            return 0;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            HandleDisplayStateChange(newState);
        });

        return 0;
    }

    /// <summary>
    /// Runs on the dispatcher thread. Compares the new state against the last
    /// seen state, fires ResumeDetected synchronously if appropriate, and
    /// schedules the debounced DisplayChanged.
    /// </summary>
    private void HandleDisplayStateChange(uint newState)
    {
        uint lastState = _lastDisplayState;
        _lastDisplayState = newState;

        if (!ShouldTriggerOnDisplayState(newState, lastState))
        {
            return;
        }

        Logger.LogInfo(
            $"[DisplayChangeWatcher] Console display ON (was {DescribeState(lastState)}); " +
            $"firing ResumeDetected and scheduling rescan");

        // Synchronously notify subscribers that a wake transition has happened
        // BEFORE we start the debounce — gives the ViewModel a chance to lock
        // interactive UI for the entire rediscovery window.
        ResumeDetected?.Invoke(this, EventArgs.Empty);

        ScheduleDisplayChanged();
    }

    private static string DescribeState(uint state) => state switch
    {
        PowerSettingsNative.DisplayStateOff => "off",
        PowerSettingsNative.DisplayStateOn => "on",
        PowerSettingsNative.DisplayStateDimmed => "dimmed",
        _ => $"unknown(0x{state:X})",
    };

    private void CleanupDeviceWatcher()
    {
        if (_deviceWatcher != null)
        {
            try
            {
                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
                _deviceWatcher.Stopped -= OnWatcherStopped;
            }
            catch
            {
                // Ignore cleanup errors.
            }

            _deviceWatcher = null;
        }
    }

    private void ScheduleDisplayChanged()
    {
        CancelDebounce();

        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceDelay, token);

                if (!token.IsCancellationRequested)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        if (!_disposed)
                        {
                            DisplayChanged?.Invoke(this, EventArgs.Empty);
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected — a newer event superseded this one.
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DisplayChangeWatcher] Error in debounce task: {ex.Message}");
            }
        });
    }

    private void CancelDebounce()
    {
        try
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_displayStateRegistration != IntPtr.Zero)
        {
            try
            {
                PowerSettingsNative.PowerSettingUnregisterNotification(_displayStateRegistration);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DisplayChangeWatcher] Unregister failed: {ex.Message}");
            }

            _displayStateRegistration = IntPtr.Zero;
        }

        if (_displayStateCallbackHandle.IsAllocated)
        {
            _displayStateCallbackHandle.Free();
        }

        Stop();
        CleanupDeviceWatcher();
        CancelDebounce();
    }
}
```

**What changed vs. the prior file** (for reviewer's sanity, not a separate step):
- Removed `using Microsoft.Win32;` (no longer needed)
- Removed `SystemEvents.PowerModeChanged += OnPowerModeChanged;` from constructor
- Removed `OnPowerModeChanged` method entirely
- Removed `SystemEvents.PowerModeChanged -= OnPowerModeChanged;` from `Dispose`
- Added `RegisterDisplayStateNotification()` call in constructor
- Added `_displayStateRegistration`, `_displayStateCallback`, `_displayStateCallbackHandle`, `_lastDisplayState` fields
- Added `ResumeDetected` event
- Added `ShouldTriggerOnDisplayState` static helper (internal for test access)
- Added `OnDisplayStateChangedNative` + `HandleDisplayStateChange` + `DescribeState`
- `Dispose` unregisters notification, frees `GCHandle`, then runs original cleanup

- [ ] **Step 4: Add InternalsVisibleTo so the test project can reach `ShouldTriggerOnDisplayState`**

Open `src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj` and locate the existing `<ItemGroup>` containing `<PackageReference>` entries. Add a new `<ItemGroup>` (or append to one already containing `<AssemblyAttribute>`) with:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="PowerDisplay.UnitTests" />
</ItemGroup>
```

If `PowerDisplay.csproj` uses `<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">` instead (newer-style), use that form. Verify which is used by inspecting the file before edit.

- [ ] **Step 5: Run the unit tests — expect them to pass**

```powershell
dotnet test src\modules\powerdisplay\PowerDisplay.UnitTests\PowerDisplay.UnitTests.csproj -c Debug -p:Platform=x64
```
Expected: all 5 tests in `DisplayStateTransitionTests` pass.

- [ ] **Step 6: Build the full PowerDisplay project to catch any consuming sites**

```powershell
dotnet build src\modules\powerdisplay\PowerDisplay\PowerDisplay.csproj -c Debug -p:Platform=x64
```
Expected: build succeeds. If you see errors about `Microsoft.Win32.SystemEvents` or `OnPowerModeChanged` from elsewhere, search for any other reference and remove it — `DisplayChangeWatcher` was the only caller, so this should not happen, but verifying surfaces any surprise.

- [ ] **Step 7: Commit**

```powershell
git add src/modules/powerdisplay/PowerDisplay/Helpers/DisplayChangeWatcher.cs
git add src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj
git add src/modules/powerdisplay/PowerDisplay.UnitTests/DisplayStateTransitionTests.cs
git commit -m "$(cat <<'EOF'
feat(PowerDisplay): subscribe GUID_CONSOLE_DISPLAY_STATE for rescan signal

Replaces SystemEvents.PowerModeChanged with a Win32 power-setting
notification subscribed via PowerSettingRegisterNotification. The new
signal fires reliably on both S3 and S0ix devices and also covers
idle-blank / lid recovery scenarios that never enter system sleep.
Introduces a ResumeDetected event so the ViewModel can lock interactive
UI synchronously, closing the debounce-window race where users could
operate on stale DDC/CI handles after wake.
EOF
)"
```

---

## Task 3: MainViewModel — lock UI on ResumeDetected

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs:113-114`

- [ ] **Step 1: Wire up the ResumeDetected subscription**

Open `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs`. Find the existing block at lines 109-114:

```csharp
        // Initialize display change watcher for auto-refresh on monitor plug/unplug
        // Use MonitorRefreshDelay from settings to allow hardware to stabilize after plug/unplug
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        int delaySeconds = Math.Clamp(settings?.Properties?.MonitorRefreshDelay ?? 5, 1, 30);
        _displayChangeWatcher = new DisplayChangeWatcher(_dispatcherQueue, TimeSpan.FromSeconds(delaySeconds));
        _displayChangeWatcher.DisplayChanged += OnDisplayChanged;
```

Replace with:

```csharp
        // Initialize display change watcher for auto-refresh on monitor plug/unplug
        // Use MonitorRefreshDelay from settings to allow hardware to stabilize after plug/unplug
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
        int delaySeconds = Math.Clamp(settings?.Properties?.MonitorRefreshDelay ?? 5, 1, 30);
        _displayChangeWatcher = new DisplayChangeWatcher(_dispatcherQueue, TimeSpan.FromSeconds(delaySeconds));
        _displayChangeWatcher.DisplayChanged += OnDisplayChanged;
        _displayChangeWatcher.ResumeDetected += OnResumeDetected;
```

- [ ] **Step 2: Add the OnResumeDetected handler**

In the same file, find `OnDisplayChanged` at around line 373:

```csharp
    private async void OnDisplayChanged(object? sender, EventArgs e)
    {
        // Set scanning state to provide visual feedback
        IsScanning = true;

        // Perform refresh - DisplayChangeWatcher has already waited for hardware to stabilize
        await RefreshMonitorsAsync(skipScanningCheck: true);
    }
```

Immediately above it, add the new handler:

```csharp
    /// <summary>
    /// Invoked synchronously the moment the console display transitions to ON
    /// (before the debounce delay elapses). Locks the interactive UI by
    /// setting IsScanning = true so the user cannot operate on stale DDC/CI
    /// handles between wake and the actual rediscovery pass that
    /// <see cref="OnDisplayChanged"/> will run once debounce completes.
    /// </summary>
    private void OnResumeDetected(object? sender, EventArgs e)
    {
        if (!IsScanning)
        {
            Logger.LogInfo("[MainViewModel] Wake detected — locking UI ahead of rediscovery");
            IsScanning = true;
        }
    }
```

- [ ] **Step 3: Detach handlers in Dispose**

`MainViewModel.cs` currently relies on `DisplayChangeWatcher.Dispose()` setting an internal `_disposed` flag that gates every callback. The pattern works but is fragile — the new `ResumeDetected` event fires **synchronously** on the dispatcher (it is not wrapped in a `TryEnqueue`), so any disposal-ordering surprise could land us in `OnResumeDetected` after the ViewModel has already cleaned up other fields. Defensively unsubscribe both handlers immediately before disposing the watcher.

Find the existing block in `MainViewModel.cs` (around line 269-275):

```csharp
        try
        {
            _displayChangeWatcher?.Dispose();
        }
```

Replace with:

```csharp
        try
        {
            if (_displayChangeWatcher is not null)
            {
                _displayChangeWatcher.ResumeDetected -= OnResumeDetected;
                _displayChangeWatcher.DisplayChanged -= OnDisplayChanged;
                _displayChangeWatcher.Dispose();
            }
        }
```

- [ ] **Step 4: Verify Logger import is present**

Top of `MainViewModel.cs` should already have `using ManagedCommon;` (line 15). If somehow missing, add it. The new handler uses `Logger.LogInfo`.

- [ ] **Step 5: Build the full PowerDisplay project**

```powershell
dotnet build src\modules\powerdisplay\PowerDisplay\PowerDisplay.csproj -c Debug -p:Platform=x64
```
Expected: build succeeds.

- [ ] **Step 6: Run existing test suites to confirm no regression**

```powershell
dotnet test src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.csproj -c Debug -p:Platform=x64
dotnet test src\modules\powerdisplay\PowerDisplay.UnitTests\PowerDisplay.UnitTests.csproj -c Debug -p:Platform=x64
```
Expected: all tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs
git commit -m "feat(PowerDisplay): lock UI immediately on console display wake"
```

---

## Task 4: SetVcpFeatureAsync — local retry on transient failure

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs:726-758`

- [ ] **Step 1: Replace SetVcpFeatureAsync with the retrying implementation**

Open `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`. Find the method starting at line 726:

```csharp
        /// <summary>
        /// Generic method to set VCP feature value directly.
        /// </summary>
        private Task<MonitorOperationResult> SetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            int value,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            return Task.Run(
                () =>
                {
                    if (monitor.Handle == IntPtr.Zero)
                    {
                        return MonitorOperationResult.Failure("Invalid monitor handle");
                    }

                    try
                    {
                        if (SetVCPFeature(monitor.Handle, vcpCode, (uint)value))
                        {
                            return MonitorOperationResult.Success();
                        }

                        var lastError = Marshal.GetLastWin32Error();
                        return MonitorOperationResult.Failure($"Failed to set VCP 0x{vcpCode:X2}", lastError);
                    }
                    catch (Exception ex)
                    {
                        return MonitorOperationResult.Failure($"Exception setting VCP 0x{vcpCode:X2}: {ex.Message}");
                    }
                },
                cancellationToken);
        }
```

Replace with:

```csharp
        /// <summary>
        /// Generic method to set VCP feature value directly.
        ///
        /// Retries up to <see cref="MaxSetVcpAttempts"/> times with
        /// <see cref="SetVcpRetryDelayMs"/> backoff between attempts. The DDC/CI
        /// I²C bus is fragile around monitor power transitions — a fresh handle
        /// can still fail its first write if the bus has not yet stabilised
        /// after wake. Retry absorbs that without bothering the user. Truly
        /// stale handles are caught upstream by the
        /// <c>DisplayChangeWatcher.ResumeDetected</c> path, which locks the UI
        /// for the duration of the rediscovery; this retry is purely a
        /// tail-latency mitigation, not a substitute for handle refresh.
        /// </summary>
        private async Task<MonitorOperationResult> SetVcpFeatureAsync(
            Monitor monitor,
            byte vcpCode,
            int value,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            if (monitor.Handle == IntPtr.Zero)
            {
                return MonitorOperationResult.Failure("Invalid monitor handle");
            }

            int lastError = 0;
            string? lastMessage = null;

            for (int attempt = 1; attempt <= MaxSetVcpAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    bool ok = await Task.Run(
                        () => SetVCPFeature(monitor.Handle, vcpCode, (uint)value),
                        cancellationToken);

                    if (ok)
                    {
                        if (attempt > 1)
                        {
                            Logger.LogInfo(
                                $"DDC: SetVCPFeature(VCP=0x{vcpCode:X2}) succeeded on attempt {attempt}");
                        }

                        return MonitorOperationResult.Success();
                    }

                    lastError = Marshal.GetLastWin32Error();
                    lastMessage = $"Failed to set VCP 0x{vcpCode:X2}";
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = 0;
                    lastMessage = $"Exception setting VCP 0x{vcpCode:X2}: {ex.Message}";
                }

                if (attempt < MaxSetVcpAttempts)
                {
                    Logger.LogWarning(
                        $"DDC: SetVCPFeature(VCP=0x{vcpCode:X2}) attempt {attempt} failed " +
                        $"(lastError=0x{lastError:X}); retrying in {SetVcpRetryDelayMs}ms");

                    await Task.Delay(SetVcpRetryDelayMs, cancellationToken);
                }
            }

            return lastError == 0
                ? MonitorOperationResult.Failure(lastMessage ?? $"Failed to set VCP 0x{vcpCode:X2}")
                : MonitorOperationResult.Failure(lastMessage ?? $"Failed to set VCP 0x{vcpCode:X2}", lastError);
        }
```

- [ ] **Step 2: Add the constants near the top of the class**

In the same file (`DdcCiController.cs`), find the existing constants section (look for `const int maxAttempts = 3;` inside `FetchCapabilitiesWithFallbackAsync` around line 335 — we want our class-level constants to be visually adjacent to the other DDC constants like `VcpCodeInputSource`). Locate the field declarations area near the top of the class (search for `private const byte VcpCodeInputSource`); add:

```csharp
        // Retry tuning for user-initiated VCP writes. The bus typically
        // re-stabilises within ~100-300ms of a fresh handle, so 3 attempts at
        // 200ms cover the slow tail without user-visible latency.
        private const int MaxSetVcpAttempts = 3;
        private const int SetVcpRetryDelayMs = 200;
```

If the file uses `internal const` for similar constants, match that style. If the constants would create duplicate names with anything elsewhere, prefix with `SetVcp_` and update the references in Step 1 accordingly.

- [ ] **Step 3: Build the Lib project**

```powershell
dotnet build src\modules\powerdisplay\PowerDisplay.Lib\PowerDisplay.Lib.csproj -c Debug -p:Platform=x64
```
Expected: build succeeds. If `await Task.Run(...)` triggers a warning about `Task.Run` capture of `monitor.Handle`, that's expected — `Handle` is a value-type `IntPtr`, captured by value at Task.Run invocation.

- [ ] **Step 4: Run the Lib unit tests to confirm no regression**

```powershell
dotnet test src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.csproj -c Debug -p:Platform=x64
```
Expected: all existing tests pass. (No new tests for this method — single-API retry with native calls is not unit-testable without dependency injection, deferred to manual verification.)

- [ ] **Step 5: Commit**

```powershell
git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
git commit -m "$(cat <<'EOF'
fix(PowerDisplay): retry SetVCPFeature 3× to absorb transient I²C failures

User-initiated VCP writes previously failed silently on a single I²C
failure, which commonly happens around monitor power transitions even
when the handle itself is fresh. Three attempts spaced 200ms apart give
the bus time to stabilise. Stale-handle scenarios are still handled
upstream by the DisplayChangeWatcher.ResumeDetected UI lock — this
retry only covers transient bus errors on otherwise-valid handles.
EOF
)"
```

---

## Task 5: Manual verification checklist

This task produces no code — it appends a verification section to the existing manual test doc so the user-facing scenarios are reproducible and reviewable.

**Files:**
- Modify: `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md` (append a new section)

- [ ] **Step 1: Append the new section**

Open `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md`. Add this section after the existing "## 6. Clean up" section (before the trailing "What this verification does NOT cover" if present, or at the very end of the file):

```markdown
---

## 7. Display-state rescan + VCP write retry (sleep/wake recovery)

This section verifies the changes from
`docs/superpowers/plans/2026-05-13-powerdisplay-display-state-rescan.md`:

- `GUID_CONSOLE_DISPLAY_STATE` replaces `SystemEvents.PowerModeChanged`.
- UI locks immediately on display-on transition, ahead of the debounce delay.
- `SetVCPFeature` retries 3× with 200ms spacing.

Run each subsection on **a representative S3 device** (`powercfg /a` shows
S3 available, S0ix unavailable). If a second machine is available with the
opposite power profile (S0ix available), repeat sections 7.2 and 7.3 there
as a separate pass.

### 7.1 Cold-boot baseline — no false wake-trigger

- [ ] Launch PowerToys; open the PowerDisplay flyout.
- [ ] In `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\<version>\log.txt`,
      look for `[DisplayChangeWatcher] Subscribed to GUID_CONSOLE_DISPLAY_STATE`.
- [ ] **Negative check:** the log must NOT contain
      `[DisplayChangeWatcher] Console display ON (was on); ...` —
      the subscription's initial state echo (if any) must not trigger a
      rescan because `_lastDisplayState` is seeded to `DisplayStateOn`.
- [ ] Confirm monitors appear normally after the initial discovery.

### 7.2 System sleep / resume

- [ ] Open the flyout, ensure at least one external monitor is detected
      with controls visible (brightness or input source).
- [ ] Run `powercfg /s SCHEME_CURRENT` or use Start menu → Sleep to enter S3.
- [ ] Wait ≥ 30 seconds. Wake the machine (mouse / keyboard).
- [ ] **Within 1 second** of seeing the desktop, observe the PowerDisplay
      flyout:
  - The flyout must show the "Scanning monitors..." spinner — proving
    `IsScanning = true` fired synchronously, well before the 5-second
    debounce.
  - All previously-interactive controls must be disabled (greyed out).
- [ ] Wait for the spinner to clear (~5-10s depending on
      `MonitorRefreshDelay` setting and hardware).
- [ ] Verify the log shows in order:
  1. `[DisplayChangeWatcher] Console display ON (was off); firing ResumeDetected and scheduling rescan`
  2. `[MainViewModel] Wake detected — locking UI ahead of rediscovery`
  3. `DDC: Discovery start — ... candidate handles, ... external targets`
  4. `DDC: Discovery complete in ...ms — N/M monitors`
- [ ] Verify input-source switch (or any other control) works on the first
      try after the spinner clears.

### 7.3 Idle blank → wake (system did NOT sleep)

This is the scenario the user originally reported.

- [ ] In Settings → System → Power & battery → Screen and sleep, set
      "On battery / On AC: Turn off screen after" to **1 minute** for the
      duration of this test.
- [ ] Open the PowerDisplay flyout; verify monitors are interactive.
- [ ] Walk away for >1 minute. Screen turns off but system does NOT sleep
      (verify by checking system-time clock continues; this is **idle blank
      only**).
- [ ] Move the mouse or press a key to wake the display.
- [ ] **Expected — this is the bug being fixed:** the flyout transitions
      to the scanning spinner immediately, performs a rediscovery, and
      restores interactive controls with fresh handles.
- [ ] Verify log entries match section 7.2 step 5 — `Console display ON
      (was off)` must appear.
- [ ] **Negative control:** before this fix, the log would NOT contain a
      `Console display ON` line in this scenario because the underlying
      signal source (`PowerModeChanged.Resume`) does not fire when system
      never sleeps. Confirm the new behaviour by checking the log.
- [ ] Restore the original screen-off timeout when finished.

### 7.4 Laptop lid open / close (laptops only)

- [ ] Settings → System → Power & battery → Lid behaviour: configure
      "When I close the lid: Turn off the display" (NOT "Sleep").
- [ ] Open flyout, close lid → screen off → wait 10s → open lid.
- [ ] Same expected outcome as 7.3: UI locks on lid open, rediscovers,
      and restores interactive controls.
- [ ] Restore original lid behaviour.

### 7.5 SetVCPFeature retry — induced bus failure

This step is hard to reproduce reliably without specialised hardware. If you
have a monitor known to fail VCP writes occasionally right after wake,
exercise it here. Otherwise, this is a code-review-only check:

- [ ] In a debug build, breakpoint `SetVcpFeatureAsync` and step through
      one user-initiated brightness/input change. Confirm the retry loop
      executes correctly on a forced `SetVCPFeature returned false`
      (modify the local result to false during stepping).
- [ ] Verify log shows `DDC: SetVCPFeature(VCP=0xNN) attempt 1 failed
      (lastError=0xN); retrying in 200ms`.
- [ ] Verify subsequent successful attempt logs `DDC: SetVCPFeature(VCP=0xNN)
      succeeded on attempt 2`.

### 7.6 Regression — no double-rescan or thrash

- [ ] With the flyout open, hot-plug a monitor in/out three times rapidly.
- [ ] Verify only one rescan triggers after the debounce settles (proves
      `ScheduleDisplayChanged` debounce still coalesces).
- [ ] Toggle screen off/on three times rapidly via Win+L lock screen.
- [ ] Verify same coalescing — UI locks, debounce settles, one rediscovery.
- [ ] Check Task Manager: PowerToys.PowerDisplay.exe CPU usage idles back
      to near-zero within 5s after the last event.

### 7.7 Modern Standby (S0ix) — opportunistic

If you have access to a Modern Standby device (Surface, recent Intel Evo
laptop, etc.) where `powercfg /a` reports `Standby (S0 Low Power Idle)
Network Connected` as available:

- [ ] Repeat section 7.2. The expected behaviour is **identical** to S3 —
      this is the whole point of switching signal sources. If the spinner
      does not appear on wake or the log does not show `Console display
      ON`, the fix has regressed on S0ix.
```

- [ ] **Step 2: Commit**

```powershell
git add POWERDISPLAY_MAXCOMPAT_VERIFICATION.md
git commit -m "docs(PowerDisplay): manual verification for display-state rescan + VCP retry"
```

---

## Done criteria

The implementation is complete when:

1. All 5 unit tests in `DisplayStateTransitionTests` pass.
2. All existing tests in `PowerDisplay.Lib.UnitTests` still pass.
3. Both `PowerDisplay` and `PowerDisplay.Lib` build clean on `x64|Debug` and `x64|Release`.
4. Section 7.2 (system S3 sleep/wake) and 7.3 (idle-blank wake) of the
   manual checklist both pass on the verifying engineer's S3 device.
5. `git grep -n PowerModeChanged src/modules/powerdisplay` returns zero
   matches in the modified source files — the legacy path is gone.
6. `git grep -n 'using Microsoft.Win32;' src/modules/powerdisplay/PowerDisplay/Helpers/DisplayChangeWatcher.cs` returns zero matches — the unused import is gone.

If section 7.7 (Modern Standby) is also exercised, that constitutes
additional coverage but is not required for done.

---

## Notes for the executing engineer

**Why we don't refresh the handle inside SetVcpFeatureAsync.** The
upstream `DisplayChangeWatcher.ResumeDetected` → `IsScanning = true` →
debounced rediscovery flow ensures that by the time the user can
interact with a control again, the handle stored on `Monitor.Handle` is
fresh. The retry inside `SetVcpFeatureAsync` exists only to absorb
transient I²C errors on an otherwise-valid handle — not stale-handle
recovery. If a future edge case proves stale handles can reach
`SetVcpFeatureAsync` after this change, the right response is to fix
the upstream signal path, not to add per-VCP handle refresh (which
would couple the controller to the discovery pipeline in an awkward
way).

**Callback thread safety.** The `OnDisplayStateChangedNative` callback
runs on a Windows-internal worker thread. The pattern is: read the
minimum needed from native memory there, dispatch a closure to the
DispatcherQueue, do everything else on the UI thread. Don't be tempted
to short-circuit by touching `_lastDisplayState` from the worker thread
— the dispatcher queue makes the state-machine update naturally
serialised against `Dispose`.

**`_lastDisplayState` initial value.** Seeded to `DisplayStateOn` on
purpose — Windows often (but not always) echoes the current value to
freshly-registered subscribers, and we don't want that initial echo to
trigger a spurious rediscovery on app startup. If the screen happens to
be off at startup (e.g., user launched PowerToys via a remote shell
while their physical screen sleeps), the next on-transition still fires
the rescan correctly. The cost of being wrong about the seed is
"missed one rescan trigger", which is recoverable via any subsequent
state change.

**GCHandle protection.** `Marshal.GetFunctionPointerForDelegate` does
NOT root the delegate against GC. Without `GCHandle.Alloc`, the delegate
can be collected before Windows invokes it, producing an access
violation in `DisplayChangeWatcher` callbacks. The `_displayStateCallbackHandle`
field is exclusively for keeping the delegate alive — it must be `Free()`-d
in `Dispose` to avoid a leak.
