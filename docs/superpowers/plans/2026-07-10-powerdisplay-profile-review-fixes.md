# PowerDisplay Profile Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix stale LightSwitch references, eliminate PowerDisplay's direct LightSwitch settings write, and move all UI-originated profile transactions off UI threads while showing explicit loading states.

**Architecture:** Keep `ProfileStore` as the synchronous owner of named-mutex transactions, expose worker-thread async facades, and serialize profile operations per ViewModel. Centralize LightSwitch reference reconciliation and runner-IPC persistence in `Settings.UI.Library`; the PowerDisplay process retains read-only name fallback and never writes LightSwitch settings.

**Tech Stack:** C# 13, .NET 10, WinUI 3, CommunityToolkit.Mvvm, MSTest, named `Mutex`, System.Text.Json source generation, PowerToys runner settings IPC.

## Global Constraints

- Keep the existing `Local\PowerToys_PowerDisplay_Profiles` mutex and same-directory atomic file replacement.
- Once a profile transaction starts, let it complete even if cancellation is requested.
- Do not add reverse IPC from PowerDisplay to the runner.
- Persist migrated LightSwitch references through the existing Settings UI runner callback only.
- A successful empty profile load clears stale references; a failed load never reconciles references.
- Show localized loading UI in the PowerDisplay flyout, PowerDisplay Settings page, and LightSwitch Settings page.
- On load failure, log the existing error, leave the profile UI empty, and do not add retry UI.
- Do not address unrelated review suggestions.
- Use existing repository build scripts and `vstest.console.exe`; do not use `dotnet test`.

---

## File Structure

**Create**

- `src\settings-ui\Settings.UI.Library\LightSwitchProfileSettingsUpdater.cs` - Reconcile LightSwitch profile references and send one runner settings message.
- `src\modules\powerdisplay\PowerDisplay.Models\ProfileOperationCoordinator.cs` - Serialize async profile operations and expose loading state.
- `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileSettingsUpdaterTests.cs` - Verify IPC emission semantics.
- `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileOperationCoordinatorTests.cs` - Verify loading state and operation serialization.

**Modify**

- `src\settings-ui\Settings.UI.Library\LightSwitchProfileResolver.cs` - Reconcile legacy names and stale positive ids.
- `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileResolverTests.cs` - Cover empty successful loads and stale ids.
- `src\modules\powerdisplay\PowerDisplay.Models\ProfileStore.cs` - Add worker-thread async transaction entry points.
- `src\modules\powerdisplay\PowerDisplay.Models\ProfileHelper.cs` - Expose async profile operations.
- `src\modules\powerdisplay\PowerDisplay.Lib\Services\ProfileService.cs` - Forward async app operations.
- `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileStoreTests.cs` - Verify async mutex waiting and atomic concurrent updates.
- `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.cs` - Start async profile loading and expose loading state.
- `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Settings.cs` - Convert refresh, apply lookup, migration, and telemetry paths.
- `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Monitors.cs` - Schedule side-file profile migration off the UI thread.
- `src\modules\powerdisplay\PowerDisplay\PowerDisplayXAML\MainWindow.xaml` - Show flyout profile loading UI.
- `src\modules\powerdisplay\PowerDisplay\Strings\en-us\Resources.resw` - Add localized loading text and automation name.
- `src\settings-ui\Settings.UI\ViewModels\PowerDisplayViewModel.cs` - Add async initialization, migration, and CRUD.
- `src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml` - Show loading card and disable profile actions.
- `src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml.cs` - Await initialization and CRUD.
- `src\settings-ui\Settings.UI\ViewModels\LightSwitchViewModel.cs` - Add async loading, reconciliation, and binding suppression.
- `src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml` - Replace selectors with loading state while busy.
- `src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml.cs` - Await profile initialization on page load.
- `src\settings-ui\Settings.UI\Strings\en-us\Resources.resw` - Add shared Settings UI loading text and automation name.

---

### Task 1: Reconcile LightSwitch References and Send Runner IPC

**Files:**
- Create: `src\settings-ui\Settings.UI.Library\LightSwitchProfileSettingsUpdater.cs`
- Create: `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileSettingsUpdaterTests.cs`
- Modify: `src\settings-ui\Settings.UI.Library\LightSwitchProfileResolver.cs:60-105`
- Modify: `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileResolverTests.cs:72-98`

**Interfaces:**
- Produces: `LightSwitchProfileResolver.ReconcileReferences(LightSwitchProperties, PowerDisplayProfiles) -> bool`
- Produces: `LightSwitchProfileSettingsUpdater.ReconcileAndSend(LightSwitchSettings, PowerDisplayProfiles, Func<string, int>) -> bool`
- Consumes: `SndLightSwitchSettings` and `SndModuleSettings<SndLightSwitchSettings>` for the existing runner envelope.

- [ ] **Step 1: Add failing stale-reference reconciliation tests**

Add these tests to `LightSwitchProfileResolverTests.cs`:

```csharp
[TestMethod]
public void ReconcileReferences_StalePositiveIdWithEmptyProfiles_ClearsReference()
{
    var profiles = Profiles();
    var props = new LightSwitchProperties();
    props.DarkModeProfileId.Value = 7;
    props.DarkModeProfile.Value = "Night";

    Assert.IsTrue(LightSwitchProfileResolver.ReconcileReferences(props, profiles));
    Assert.AreEqual(0, props.DarkModeProfileId.Value);
    Assert.AreEqual(string.Empty, props.DarkModeProfile.Value);
}

[TestMethod]
public void ReconcileReferences_ValidPositiveId_DoesNotChangeReference()
{
    var profiles = Profiles(("Night", 7));
    var props = new LightSwitchProperties();
    props.DarkModeProfileId.Value = 7;
    props.DarkModeProfile.Value = "Night";

    Assert.IsFalse(LightSwitchProfileResolver.ReconcileReferences(props, profiles));
    Assert.AreEqual(7, props.DarkModeProfileId.Value);
    Assert.AreEqual("Night", props.DarkModeProfile.Value);
}

[TestMethod]
public void ReconcileReferences_StalePositiveIdWithOtherProfiles_ClearsReference()
{
    var profiles = Profiles(("Day", 4));
    var props = new LightSwitchProperties();
    props.DarkModeProfileId.Value = 7;
    props.DarkModeProfile.Value = "Night";

    Assert.IsTrue(LightSwitchProfileResolver.ReconcileReferences(props, profiles));
    Assert.AreEqual(0, props.DarkModeProfileId.Value);
    Assert.AreEqual(string.Empty, props.DarkModeProfile.Value);
}

[TestMethod]
public void ReconcileReferences_EmptyReference_RemainsUnchanged()
{
    var props = new LightSwitchProperties();

    Assert.IsFalse(LightSwitchProfileResolver.ReconcileReferences(props, Profiles()));
    Assert.AreEqual(0, props.LightModeProfileId.Value);
    Assert.AreEqual(string.Empty, props.LightModeProfile.Value);
}
```

- [ ] **Step 2: Add failing runner-IPC updater tests**

Create `LightSwitchProfileSettingsUpdaterTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileSettingsUpdaterTests
{
    [TestMethod]
    public void ReconcileAndSend_ChangedReference_SendsExactlyOneMessage()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfile.Value = "Night";
        var profiles = Profiles(("Night", 7));
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            profiles,
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, messages.Count);
        StringAssert.Contains(messages[0], "\"darkModeProfileId\":{\"value\":7}");
    }

    [TestMethod]
    public void ReconcileAndSend_UnchangedReference_DoesNotSend()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.DarkModeProfile.Value = "Night";
        var profiles = Profiles(("Night", 7));
        var sendCount = 0;

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            profiles,
            _ =>
            {
                sendCount++;
                return 0;
            });

        Assert.IsFalse(changed);
        Assert.AreEqual(0, sendCount);
    }

    [TestMethod]
    public void ReconcileAndSend_EmptyProfilesClearStaleId_SendsExactlyOneMessage()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.DarkModeProfile.Value = "Night";
        var messages = new List<string>();

        var changed = LightSwitchProfileSettingsUpdater.ReconcileAndSend(
            settings,
            Profiles(),
            message =>
            {
                messages.Add(message);
                return 0;
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(0, settings.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(string.Empty, settings.Properties.DarkModeProfile.Value);
    }

    private static PowerDisplayProfiles Profiles(params (string Name, int Id)[] items)
    {
        var profiles = new PowerDisplayProfiles();
        foreach (var (name, id) in items)
        {
            profiles.Profiles.Add(new PowerDisplayProfile(
                name,
                new List<ProfileMonitorSetting>
                {
                    new ProfileMonitorSetting("MON1", 50, null, null, null),
                })
            {
                Id = id,
            });
        }

        return profiles;
    }
}
```

- [ ] **Step 3: Run the new tests and verify they fail**

Run:

```powershell
Set-Location src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' `
  '.\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  '/TestCaseFilter:FullyQualifiedName~LightSwitchProfileResolverTests|FullyQualifiedName~LightSwitchProfileSettingsUpdaterTests'
```

Expected: build fails because `ReconcileReferences` and `LightSwitchProfileSettingsUpdater` do not exist.

- [ ] **Step 4: Implement reference reconciliation**

Replace `MigrateNamesToIds` and `MigrateOne` in `LightSwitchProfileResolver.cs` with:

```csharp
public static bool ReconcileReferences(LightSwitchProperties props, PowerDisplayProfiles profiles)
{
    ArgumentNullException.ThrowIfNull(props);
    ArgumentNullException.ThrowIfNull(profiles);

    var changed = false;
    changed |= ReconcileOne(profiles, props.LightModeProfileId, props.LightModeProfile);
    changed |= ReconcileOne(profiles, props.DarkModeProfileId, props.DarkModeProfile);
    return changed;
}

public static bool MigrateNamesToIds(LightSwitchProperties props, PowerDisplayProfiles profiles)
    => ReconcileReferences(props, profiles);

private static bool ReconcileOne(PowerDisplayProfiles profiles, IntProperty idProp, StringProperty nameProp)
{
    if (idProp.Value >= 1)
    {
        if (profiles.GetById(idProp.Value) is not null)
        {
            return false;
        }

        idProp.Value = 0;
        nameProp.Value = string.Empty;
        return true;
    }

    var name = nameProp.Value;
    if (string.IsNullOrEmpty(name) || name == NoneSentinel)
    {
        return false;
    }

    var match = profiles.GetProfile(name);
    if (match is not null && match.Id >= 1)
    {
        idProp.Value = match.Id;
        nameProp.Value = match.Name;
    }
    else
    {
        nameProp.Value = string.Empty;
    }

    return true;
}
```

Update existing resolver tests to call `ReconcileReferences`. Keep `MigrateNamesToIds` as a compatibility alias until all stacked consumers move to the new name.

- [ ] **Step 5: Implement the shared runner-IPC updater**

Create `LightSwitchProfileSettingsUpdater.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class LightSwitchProfileSettingsUpdater
    {
        public static bool ReconcileAndSend(
            LightSwitchSettings settings,
            PowerDisplayProfiles profiles,
            Func<string, int> sendConfigMessage)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(sendConfigMessage);

            if (!LightSwitchProfileResolver.ReconcileReferences(settings.Properties, profiles))
            {
                return false;
            }

            var outgoing = new SndModuleSettings<SndLightSwitchSettings>(
                new SndLightSwitchSettings(settings));
            sendConfigMessage(outgoing.ToJsonString());
            return true;
        }
    }
}
```

- [ ] **Step 6: Run focused tests**

Run the command from Step 3 again.

Expected: all resolver and updater tests pass.

- [ ] **Step 7: Commit**

```powershell
git add -- `
  src\settings-ui\Settings.UI.Library\LightSwitchProfileResolver.cs `
  src\settings-ui\Settings.UI.Library\LightSwitchProfileSettingsUpdater.cs `
  src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileResolverTests.cs `
  src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileSettingsUpdaterTests.cs
git commit -m "fix(powerdisplay): reconcile LightSwitch profile references" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 0ad959b9-dadf-48f6-9367-a6738da252c4"
```

---

### Task 2: Add Async Profile Transactions and Loading Coordination

**Files:**
- Create: `src\modules\powerdisplay\PowerDisplay.Models\ProfileOperationCoordinator.cs`
- Create: `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileOperationCoordinatorTests.cs`
- Modify: `src\modules\powerdisplay\PowerDisplay.Models\ProfileStore.cs:26-106`
- Modify: `src\modules\powerdisplay\PowerDisplay.Models\ProfileHelper.cs:36-106`
- Modify: `src\modules\powerdisplay\PowerDisplay.Lib\Services\ProfileService.cs:16-29`
- Modify: `src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileStoreTests.cs:167-223`

**Interfaces:**
- Produces: `ProfileStore.LoadProfilesAsync`, `LoadProfilesEnsuringIdsAsync`, `AddOrUpdateProfileAsync`, `RemoveProfileByIdAsync`, and `UpdateProfilesAsync`.
- Produces: matching public methods on `ProfileHelper` and `ProfileService`.
- Produces: `ProfileOperationCoordinator.RunAsync<T>(Func<CancellationToken, Task<T>>, CancellationToken)`.
- Consumes: the existing synchronous transaction methods without changing their semantics.

- [ ] **Step 1: Add failing async store tests**

Append to `ProfileStoreTests.cs`:

```csharp
[TestMethod]
public async Task LoadProfilesEnsuringIdsAsync_WaitsWithoutBlockingCaller()
{
    var firstStore = CreateStore();
    var secondStore = CreateStore();
    using var updateLoaded = new ManualResetEventSlim();
    using var continueUpdate = new ManualResetEventSlim();

    var holder = Task.Run(() =>
        firstStore.UpdateProfiles(profiles =>
        {
            updateLoaded.Set();
            continueUpdate.Wait();
            return false;
        }));

    Assert.IsTrue(updateLoaded.Wait(TimeSpan.FromSeconds(5)));

    var waitingLoad = secondStore.LoadProfilesEnsuringIdsAsync();
    Assert.IsFalse(waitingLoad.IsCompleted);

    continueUpdate.Set();
    await holder;
    var loaded = await waitingLoad;

    Assert.IsNotNull(loaded);
}

[TestMethod]
public async Task AddOrUpdateProfileAsync_TwoStoresSharingMutex_PreserveBothUpdates()
{
    var firstStore = CreateStore();
    var secondStore = CreateStore();
    using var start = new ManualResetEventSlim();

    var first = Task.Run(async () =>
    {
        start.Wait();
        await firstStore.AddOrUpdateProfileAsync(MakeProfile("First"));
    });
    var second = Task.Run(async () =>
    {
        start.Wait();
        await secondStore.AddOrUpdateProfileAsync(MakeProfile("Second"));
    });

    start.Set();
    await Task.WhenAll(first, second);

    var loaded = await firstStore.LoadProfilesAsync();
    Assert.AreEqual(2, loaded.Profiles.Count);
    Assert.AreEqual(2, loaded.Profiles.Select(profile => profile.Id).Distinct().Count());
}
```

- [ ] **Step 2: Add failing coordinator tests**

Create `ProfileOperationCoordinatorTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class ProfileOperationCoordinatorTests
{
    [TestMethod]
    public async Task RunAsync_ReportsLoadingUntilOperationCompletes()
    {
        var coordinator = new ProfileOperationCoordinator();
        var states = new List<bool>();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.IsRunningChanged += (_, _) => states.Add(coordinator.IsRunning);

        var operation = coordinator.RunAsync(_ => completion.Task);

        Assert.IsTrue(coordinator.IsRunning);
        completion.SetResult(42);
        Assert.AreEqual(42, await operation);
        Assert.IsFalse(coordinator.IsRunning);
        CollectionAssert.AreEqual(new[] { true, false }, states);
    }

    [TestMethod]
    public async Task RunAsync_SerializesOverlappingOperations()
    {
        var coordinator = new ProfileOperationCoordinator();
        var firstCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;

        var first = coordinator.RunAsync(_ => firstCompletion.Task);
        var second = coordinator.RunAsync(_ =>
        {
            secondStarted = true;
            return Task.FromResult(2);
        });

        Assert.IsFalse(secondStarted);
        firstCompletion.SetResult(1);
        await Task.WhenAll(first, second);
        Assert.IsTrue(secondStarted);
    }

    [TestMethod]
    public async Task RunAsync_OperationFailure_ResetsLoadingState()
    {
        var coordinator = new ProfileOperationCoordinator();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => coordinator.RunAsync<int>(
                _ => Task.FromException<int>(new InvalidOperationException("failure"))));

        Assert.IsFalse(coordinator.IsRunning);
    }
}
```

- [ ] **Step 3: Run the new tests and verify they fail**

Run:

```powershell
Set-Location src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

Expected: build fails because the async methods and `ProfileOperationCoordinator` do not exist.

- [ ] **Step 4: Add async wrappers to `ProfileStore`**

Add:

```csharp
internal Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
    => RunAsync(LoadProfiles, cancellationToken);

internal Task<PowerDisplayProfiles> LoadProfilesEnsuringIdsAsync(CancellationToken cancellationToken = default)
    => RunAsync(LoadProfilesEnsuringIds, cancellationToken);

internal Task AddOrUpdateProfileAsync(
    PowerDisplayProfile profile,
    CancellationToken cancellationToken = default)
    => RunAsync(
        () =>
        {
            AddOrUpdateProfile(profile);
            return true;
        },
        cancellationToken);

internal Task<bool> RemoveProfileByIdAsync(int id, CancellationToken cancellationToken = default)
    => RunAsync(() => RemoveProfileById(id), cancellationToken);

internal Task<bool> UpdateProfilesAsync(
    Func<PowerDisplayProfiles, bool> update,
    CancellationToken cancellationToken = default)
    => RunAsync(() => UpdateProfiles(update), cancellationToken);

private static Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(operation);
    return Task.Run(operation, cancellationToken);
}
```

Add `using System.Threading.Tasks;`.

- [ ] **Step 5: Add async public facades**

Add matching methods to `ProfileHelper`:

```csharp
public static Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
    => _profileStore.Value.LoadProfilesAsync(cancellationToken);

public static Task<PowerDisplayProfiles> LoadProfilesEnsuringIdsAsync(CancellationToken cancellationToken = default)
    => _profileStore.Value.LoadProfilesEnsuringIdsAsync(cancellationToken);

public static Task AddOrUpdateProfileAsync(
    PowerDisplayProfile profile,
    CancellationToken cancellationToken = default)
    => _profileStore.Value.AddOrUpdateProfileAsync(profile, cancellationToken);

public static Task<bool> RemoveProfileByIdAsync(int id, CancellationToken cancellationToken = default)
    => _profileStore.Value.RemoveProfileByIdAsync(id, cancellationToken);

public static Task<bool> UpdateProfilesAsync(
    Func<PowerDisplayProfiles, bool> update,
    CancellationToken cancellationToken = default)
    => _profileStore.Value.UpdateProfilesAsync(update, cancellationToken);
```

Forward the load and update methods from `ProfileService` with the same signatures.

- [ ] **Step 6: Implement `ProfileOperationCoordinator`**

Create `ProfileOperationCoordinator.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Models
{
    public sealed class ProfileOperationCoordinator
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public bool IsRunning { get; private set; }

        public event EventHandler? IsRunningChanged;

        public async Task<T> RunAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            await _gate.WaitAsync(cancellationToken);
            SetIsRunning(true);
            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                SetIsRunning(false);
                _gate.Release();
            }
        }

        public async Task RunAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await RunAsync(
                async token =>
                {
                    await operation(token);
                    return true;
                },
                cancellationToken);
        }

        private void SetIsRunning(bool value)
        {
            if (IsRunning == value)
            {
                return;
            }

            IsRunning = value;
            IsRunningChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 7: Build and run focused tests**

Run:

```powershell
Set-Location src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' `
  '.\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  '/TestCaseFilter:FullyQualifiedName~ProfileStoreTests|FullyQualifiedName~ProfileOperationCoordinatorTests'
```

Expected: all profile store and coordinator tests pass.

- [ ] **Step 8: Commit**

```powershell
git add -- `
  src\modules\powerdisplay\PowerDisplay.Models\ProfileStore.cs `
  src\modules\powerdisplay\PowerDisplay.Models\ProfileHelper.cs `
  src\modules\powerdisplay\PowerDisplay.Models\ProfileOperationCoordinator.cs `
  src\modules\powerdisplay\PowerDisplay.Lib\Services\ProfileService.cs `
  src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileStoreTests.cs `
  src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\ProfileOperationCoordinatorTests.cs
git commit -m "refactor(powerdisplay): add async profile transactions" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 0ad959b9-dadf-48f6-9367-a6738da252c4"
```

---

### Task 3: Move PowerDisplay App Profile Work Off the UI Thread

**Files:**
- Modify: `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.cs:81-128,247-251,457-478`
- Modify: `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Settings.cs:90-132,156-249,575-698,753-770`
- Modify: `src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Monitors.cs:24-64,170-180`
- Modify: `src\modules\powerdisplay\PowerDisplay\PowerDisplayXAML\MainWindow.xaml:697-731`
- Modify: `src\modules\powerdisplay\PowerDisplay\Strings\en-us\Resources.resw:248-255`

**Interfaces:**
- Consumes: `ProfileOperationCoordinator`.
- Consumes: async methods from `ProfileService`.
- Produces: `MainViewModel.IsProfilesLoading`, `InitializeProfilesAsync`, and asynchronous profile lookup/migration.

- [ ] **Step 1: Establish the source-level failing checks**

Run:

```powershell
rg -n "LoadProfiles\\(\\)|ProfileService\\.(LoadProfiles|LoadProfilesEnsuringIds|UpdateProfiles)\\(" `
  src\modules\powerdisplay\PowerDisplay\ViewModels
rg -n "GetSettingsOrDefault<LightSwitchSettings>|SaveSettings\\(.*LightSwitch" `
  src\modules\powerdisplay\PowerDisplay
```

Expected: synchronous UI call sites and app-side LightSwitch settings persistence are reported.

- [ ] **Step 2: Add loading state and asynchronous initialization**

In `MainViewModel.cs`, add:

```csharp
private readonly ProfileOperationCoordinator _profileOperations = new();

public bool IsProfilesLoading => _profileOperations.IsRunning;

public bool ShowProfileSwitcherButton =>
    ShowProfileSwitcher && (HasProfiles || IsProfilesLoading);
```

Subscribe in the constructor before starting work:

```csharp
_profileOperations.IsRunningChanged += (_, _) =>
{
    OnPropertyChanged(nameof(IsProfilesLoading));
    OnPropertyChanged(nameof(ShowProfileSwitcherButton));
};

_ = InitializeProfilesAsync(_cancellationTokenSource.Token);
```

Remove the synchronous constructor call to `LoadProfiles`.

Implement:

```csharp
private async Task InitializeProfilesAsync(CancellationToken cancellationToken = default)
{
    try
    {
        await _profileOperations.RunAsync(
            async token =>
            {
                var profilesData = await ProfileService.LoadProfilesEnsuringIdsAsync(token);
                Profiles.Clear();
                foreach (var profile in profilesData.Profiles)
                {
                    Profiles.Add(profile);
                }

                OnPropertyChanged(nameof(HasProfiles));
                OnPropertyChanged(nameof(ShowProfileSwitcherButton));
            },
            cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        Profiles.Clear();
        Logger.LogError($"[Profile] Failed to load profiles: {ex.Message}");
    }
}
```

- [ ] **Step 3: Convert refresh and apply lookup**

Change `ApplySettingsFromUI` to start an observed helper:

```csharp
_ = InitializeProfilesAsync(_cancellationTokenSource.Token);
```

Replace `LoadValidProfileById` with:

```csharp
private static async Task<PowerDisplayProfile?> LoadValidProfileByIdAsync(
    int profileId,
    string logPrefix,
    CancellationToken cancellationToken = default)
{
    var profile = (await ProfileService.LoadProfilesAsync(cancellationToken)).GetById(profileId);
    if (profile == null || !profile.IsValid())
    {
        Logger.LogWarning($"{logPrefix} Profile id {profileId} not found or invalid");
        return null;
    }

    return profile;
}
```

Await it from `ApplyProfileByIdAsync`. Change `ApplyLightSwitchProfile` so the background task begins before either settings or profile I/O:

```csharp
public void ApplyLightSwitchProfile(bool isLightMode)
{
    _ = Task.Run(async () =>
    {
        try
        {
            var profileId = LightSwitchService.GetProfileForTheme(isLightMode);
            if (profileId is null)
            {
                return;
            }

            var profile = await LoadValidProfileByIdAsync(
                profileId.Value,
                "[LightSwitch Integration]",
                _cancellationTokenSource.Token);
            if (profile == null)
            {
                return;
            }

            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_dispatcherQueue.TryEnqueue(
                () => _ = ApplyProfileAndCompleteAsync(profile.MonitorSettings, tcs)))
            {
                Logger.LogError("[LightSwitch Integration] Failed to enqueue profile application to UI thread");
                return;
            }

            await tcs.Task;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[LightSwitch Integration] Failed to apply profile: {ex.GetType().Name}: {ex.Message}");
        }
    });
}
```

- [ ] **Step 4: Remove app-side LightSwitch persistence and make side-file migration async**

Delete `BackfillProfileIds` and its call. Initial profile loading already guarantees ids.

Capture `discovered` on the UI thread, then schedule:

```csharp
_ = MigrateLegacyMonitorIdsInSideFilesAsync(discovered, _cancellationTokenSource.Token);
```

Implement the profile portion with:

```csharp
private async Task MigrateLegacyMonitorIdsInSideFilesAsync(
    List<(string Id, int MonitorNumber)> discovered,
    CancellationToken cancellationToken)
{
    try
    {
        var anyChanged = await _profileOperations.RunAsync(
            token => ProfileService.UpdateProfilesAsync(
                profiles => MigrateLegacyMonitorIds(profiles, discovered),
                token),
            cancellationToken);

        if (anyChanged)
        {
            Logger.LogInfo("[LegacyMigration] profiles.json updated with DevicePath-based monitor Ids.");
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"[LegacyMigration] Failed to migrate profiles.json: {ex.Message}");
    }

    _stateManager.MigrateLegacyKeys(discovered);
}
```

Extract the existing mutation loop:

```csharp
private static bool MigrateLegacyMonitorIds(
    PowerDisplayProfiles profiles,
    List<(string Id, int MonitorNumber)> discovered)
{
    if (profiles.Profiles is null || profiles.Profiles.Count == 0)
    {
        return false;
    }

    var changedProfiles = false;
    foreach (var profile in profiles.Profiles)
    {
        if (profile?.MonitorSettings is null)
        {
            continue;
        }

        var changed = false;
        foreach (var legacy in profile.MonitorSettings
            .Where(setting => MonitorIdentity.IsLegacyId(setting?.MonitorId))
            .ToList())
        {
            var newId = MonitorIdMigrator.MatchNewId(legacy.MonitorId, discovered);
            if (newId != null &&
                profile.MonitorSettings.All(setting => !MonitorIdComparer.Equal(setting.MonitorId, newId)))
            {
                profile.MonitorSettings.Add(new ProfileMonitorSetting(
                    newId,
                    legacy.Brightness,
                    legacy.ColorTemperatureVcp,
                    legacy.Contrast,
                    legacy.Volume));
            }
            else if (newId == null)
            {
                Logger.LogWarning(
                    $"[LegacyMigration] Dropping profile setting for '{legacy.MonitorId}' in profile '{profile.Name}': no current monitor with matching EdidId+MonitorNumber.");
            }

            profile.MonitorSettings.Remove(legacy);
            changed = true;
        }

        if (changed)
        {
            profile.Touch();
            changedProfiles = true;
        }
    }

    return changedProfiles;
}
```

Use in-memory `Profiles.Count` in `SendSettingsTelemetry` instead of reading the profile file.

- [ ] **Step 5: Add flyout loading UI**

Add these resources to the PowerDisplay `Resources.resw`:

```xml
<data name="ProfilesLoadingText.Text" xml:space="preserve">
  <value>Loading profiles...</value>
</data>
<data name="ProfilesLoadingIndicator.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name" xml:space="preserve">
  <value>Loading profiles</value>
</data>
```

Wrap the profile flyout content in a `Grid` and add:

```xml
<StackPanel
    HorizontalAlignment="Center"
    Spacing="{StaticResource FlyoutStandardSpacing}"
    Visibility="{x:Bind helpers:VisibilityConverter.BoolToVisibility(ViewModel.IsProfilesLoading), Mode=OneWay}">
    <ProgressRing
        x:Uid="ProfilesLoadingIndicator"
        Width="{StaticResource FlyoutScanningIndicatorSize}"
        Height="{StaticResource FlyoutScanningIndicatorSize}"
        IsActive="True" />
    <TextBlock x:Uid="ProfilesLoadingText" />
</StackPanel>
```

Bind the existing `ListView.Visibility` to `ViewModel.HasProfiles`.

- [ ] **Step 6: Verify app code and build**

Run the searches from Step 1 again.

Expected:

- No PowerDisplay app write or mutation of `LightSwitchSettings`.
- No synchronous `ProfileService` profile transaction from a UI entry point.

Build:

```powershell
Set-Location src\modules\powerdisplay\PowerDisplay
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

Expected: build exits 0 and XAML compilation succeeds.

- [ ] **Step 7: Commit**

```powershell
git add -- `
  src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.cs `
  src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Settings.cs `
  src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.Monitors.cs `
  src\modules\powerdisplay\PowerDisplay\PowerDisplayXAML\MainWindow.xaml `
  src\modules\powerdisplay\PowerDisplay\Strings\en-us\Resources.resw
git commit -m "fix(powerdisplay): load profiles off the UI thread" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 0ad959b9-dadf-48f6-9367-a6738da252c4"
```

---

### Task 4: Make PowerDisplay Settings Profile Operations Asynchronous

**Files:**
- Modify: `src\settings-ui\Settings.UI\ViewModels\PowerDisplayViewModel.cs:47-96,710-916`
- Modify: `src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml:155-204`
- Modify: `src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml.cs:24-36,72-143`
- Modify: `src\settings-ui\Settings.UI\Strings\en-us\Resources.resw:5598-5607`

**Interfaces:**
- Consumes: `ProfileOperationCoordinator`.
- Consumes: `ProfileHelper.LoadProfilesEnsuringIdsAsync`, `AddOrUpdateProfileAsync`, and `RemoveProfileByIdAsync`.
- Consumes: `LightSwitchProfileSettingsUpdater.ReconcileAndSend`.
- Produces: `PowerDisplayViewModel.InitializeProfilesAsync`, async CRUD methods, `IsProfilesLoading`, and `CanUseProfiles`.

- [ ] **Step 1: Add Settings UI loading resources**

Add:

```xml
<data name="PowerDisplay_ProfilesLoadingText.Text" xml:space="preserve">
  <value>Loading profiles...</value>
</data>
<data name="PowerDisplay_ProfilesLoadingIndicator.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name" xml:space="preserve">
  <value>Loading profiles</value>
</data>
```

- [ ] **Step 2: Convert ViewModel initialization**

Remove `LoadProfiles()` from the constructor. Add:

```csharp
private readonly ProfileOperationCoordinator _profileOperations = new();
private bool _suppressProfileSelectionPersistence;

public bool IsProfilesLoading => _profileOperations.IsRunning;

public bool CanUseProfiles => IsEnabled && !IsProfilesLoading;
```

Subscribe in the constructor:

```csharp
_profileOperations.IsRunningChanged += (_, _) =>
{
    OnPropertyChanged(nameof(IsProfilesLoading));
    OnPropertyChanged(nameof(CanUseProfiles));
};
```

Implement:

```csharp
public async Task InitializeProfilesAsync(CancellationToken cancellationToken = default)
{
    _suppressProfileSelectionPersistence = true;
    try
    {
        await _profileOperations.RunAsync(
            async token =>
            {
                var (loaded, lightSwitch) = await LoadProfilesCoreAsync(token);
                LightSwitchProfileSettingsUpdater.ReconcileAndSend(
                    lightSwitch,
                    loaded,
                    SendConfigMSG);
                ReplaceProfiles(loaded);
            },
            cancellationToken);
    }
    catch (Exception ex)
    {
        Profiles.Clear();
        Logger.LogError($"Failed to load profiles: {ex.Message}");
    }
}

private async Task<(PowerDisplayProfiles Profiles, LightSwitchSettings LightSwitch)> LoadProfilesCoreAsync(
    CancellationToken cancellationToken)
{
    var profiles = await ProfileHelper.LoadProfilesEnsuringIdsAsync(cancellationToken)
        .ConfigureAwait(false);
    var lightSwitch = await Task.Run(
        () => SettingsUtils.GetSettingsOrDefault<LightSwitchSettings>(
            LightSwitchSettings.ModuleName),
        cancellationToken).ConfigureAwait(false);
    return (profiles, lightSwitch);
}

private void ReplaceProfiles(PowerDisplayProfiles profilesData)
{
    Profiles.Clear();
    foreach (var profile in profilesData.Profiles)
    {
        Profiles.Add(profile);
    }

    Logger.LogInfo($"Loaded {Profiles.Count} profiles");
}
```

`LoadProfilesCoreAsync` performs both file reads on worker threads. The outer coordinator delegate resumes on the captured UI context before it calls `ReconcileAndSend` and replaces the observable collection.

- [ ] **Step 3: Convert profile CRUD**

Expose:

```csharp
public Task CreateProfileAsync(PowerDisplayProfile profile)
    => UpsertProfileAsync(profile, isNew: true);

public Task UpdateProfileAsync(PowerDisplayProfile profile)
    => UpsertProfileAsync(profile, isNew: false);

private async Task UpsertProfileAsync(PowerDisplayProfile profile, bool isNew)
{
    try
    {
        if (profile == null || !profile.IsValid())
        {
            Logger.LogWarning("Invalid profile");
            return;
        }

        await _profileOperations.RunAsync(
            async token =>
            {
                await ProfileHelper.AddOrUpdateProfileAsync(profile, token);
                var (profiles, lightSwitch) = await LoadProfilesCoreAsync(token);
                LightSwitchProfileSettingsUpdater.ReconcileAndSend(
                    lightSwitch,
                    profiles,
                    SendConfigMSG);
                ReplaceProfiles(profiles);
            });
        SignalSettingsUpdated();
    }
    catch (Exception ex)
    {
        Profiles.Clear();
        Logger.LogError($"Failed to {(isNew ? "create" : "update")} profile: {ex.Message}");
    }
}

public async Task DeleteProfileAsync(int id)
{
    try
    {
        if (id < 1)
        {
            return;
        }

        var removed = await _profileOperations.RunAsync(
            async token =>
            {
                if (!await ProfileHelper.RemoveProfileByIdAsync(id, token))
                {
                    return false;
                }

                var (profiles, lightSwitch) = await LoadProfilesCoreAsync(token);
                LightSwitchProfileSettingsUpdater.ReconcileAndSend(
                    lightSwitch,
                    profiles,
                    SendConfigMSG);
                ReplaceProfiles(profiles);
                return true;
            });
        if (!removed)
        {
            Logger.LogWarning($"Profile id {id} was not found");
            return;
        }
        SignalSettingsUpdated();
    }
    catch (Exception ex)
    {
        Profiles.Clear();
        Logger.LogError($"Failed to delete profile: {ex.Message}");
    }
}
```

Initialization and CRUD call `LoadProfilesCoreAsync` only from inside one coordinated operation, so there is no recursive semaphore acquisition and the loading state remains active through collection replacement.

Raise `OnPropertyChanged(nameof(CanUseProfiles))` whenever either `IsEnabled` or `IsProfilesLoading` changes.

- [ ] **Step 4: Await initialization and CRUD from the page**

Replace the loaded lambda with:

```csharp
Loaded += PowerDisplayPage_Loaded;
```

Add:

```csharp
private async void PowerDisplayPage_Loaded(object sender, RoutedEventArgs e)
{
    ViewModel.OnPageLoaded();
    await ViewModel.InitializeProfilesAsync();
}
```

Change event handlers to await:

```csharp
await ViewModel.CreateProfileAsync(dialog.ResultProfile);
await ViewModel.UpdateProfileAsync(dialog.ResultProfile);
await ViewModel.DeleteProfileAsync(profile.Id);
```

- [ ] **Step 5: Add Settings page loading UI**

Change the profiles `SettingsGroup` to bind `IsEnabled` to `CanUseProfiles`. Add a loading card before the expander:

```xml
<tkcontrols:SettingsCard
    HorizontalContentAlignment="Center"
    Visibility="{x:Bind ViewModel.IsProfilesLoading, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <ProgressRing
            x:Uid="PowerDisplay_ProfilesLoadingIndicator"
            Width="24"
            Height="24"
            IsActive="True" />
        <TextBlock
            x:Uid="PowerDisplay_ProfilesLoadingText"
            VerticalAlignment="Center" />
    </StackPanel>
</tkcontrols:SettingsCard>
```

Hide the existing profile expander while loading with `ReverseBoolToVisibilityConverter`.

- [ ] **Step 6: Build Settings UI**

Run:

```powershell
Set-Location src\settings-ui\Settings.UI
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

Expected: build exits 0 and `PowerDisplayPage.xaml` compiles.

- [ ] **Step 7: Commit**

```powershell
git add -- `
  src\settings-ui\Settings.UI\ViewModels\PowerDisplayViewModel.cs `
  src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml `
  src\settings-ui\Settings.UI\SettingsXAML\Views\PowerDisplayPage.xaml.cs `
  src\settings-ui\Settings.UI\Strings\en-us\Resources.resw
git commit -m "fix(settings-ui): load PowerDisplay profiles asynchronously" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 0ad959b9-dadf-48f6-9367-a6738da252c4"
```

---

### Task 5: Make LightSwitch Profile Loading Asynchronous and Safe

**Files:**
- Modify: `src\settings-ui\Settings.UI\ViewModels\LightSwitchViewModel.cs:40-65,584-781,880-884`
- Modify: `src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml:234-268`
- Modify: `src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml.cs:45-79,86-97`

**Interfaces:**
- Consumes: `ProfileOperationCoordinator`.
- Consumes: `ProfileHelper.LoadProfilesEnsuringIdsAsync`.
- Consumes: `LightSwitchProfileSettingsUpdater.ReconcileAndSend`.
- Produces: `LightSwitchViewModel.InitializeProfilesAsync` and `IsProfilesLoading`.

- [ ] **Step 1: Remove constructor I/O and add loading state**

Remove `LoadPowerDisplayProfiles()` from the constructor. Add:

```csharp
private readonly ProfileOperationCoordinator _profileOperations = new();

public bool IsProfilesLoading => _profileOperations.IsRunning;
```

Subscribe in the constructor:

```csharp
_profileOperations.IsRunningChanged += (_, _) =>
    NotifyPropertyChanged(nameof(IsProfilesLoading));
```

- [ ] **Step 2: Implement async loading and reconciliation**

Replace `LoadPowerDisplayProfiles` with:

```csharp
public async Task InitializeProfilesAsync(CancellationToken cancellationToken = default)
{
    try
    {
        await _profileOperations.RunAsync(
            async token =>
            {
                var profilesData = await ProfileHelper.LoadProfilesEnsuringIdsAsync(token);
                LightSwitchProfileSettingsUpdater.ReconcileAndSend(
                    ModuleSettings,
                    profilesData,
                    SendConfigMSG);

                AvailableProfiles.Clear();
                foreach (var profile in profilesData.Profiles)
                {
                    AvailableProfiles.Add(profile);
                }

                SelectByStoredReference(isDarkMode: true);
                SelectByStoredReference(isDarkMode: false);
            },
            cancellationToken);
    }
    catch (Exception ex)
    {
        AvailableProfiles.Clear();
        SetSelectedProfilesWithoutPersisting(null, null);
        Logger.LogError($"Failed to load PowerDisplay profiles: {ex.Message}");
    }
    finally
    {
        _suppressProfileSelectionPersistence = false;
    }
}
```

Add:

```csharp
private void SetSelectedProfilesWithoutPersisting(
    PowerDisplayProfile? darkProfile,
    PowerDisplayProfile? lightProfile)
{
    _selectedDarkModeProfile = darkProfile;
    _selectedLightModeProfile = lightProfile;
    NotifyPropertyChanged(nameof(SelectedDarkModeProfile));
    NotifyPropertyChanged(nameof(SelectedLightModeProfile));
}
```

- [ ] **Step 3: Suppress binding writes while loading**

In `SetSelectedProfile`, after assigning `field`, add:

```csharp
if (_suppressProfileSelectionPersistence || IsProfilesLoading)
{
    NotifyPropertyChanged(propertyName);
    return;
}
```

Make `SelectByStoredReference` selection-only. Delete the stale-reference mutation block at current lines 743-763. The updater has already reconciled settings after a successful load, including an empty collection.

- [ ] **Step 4: Await initialization from the page**

Change `LightSwitchPage_Loaded` to:

```csharp
private async void LightSwitchPage_Loaded(object sender, RoutedEventArgs e)
{
    await ViewModel.InitializeProfilesAsync();

    if (ViewModel.SearchLocations.Count == 0)
    {
        foreach (var city in SearchLocationLoader.GetAll())
        {
            ViewModel.SearchLocations.Add(city);
        }
    }

    ViewModel.InitializeScheduleMode();
}
```

- [ ] **Step 5: Add LightSwitch loading UI**

Inside `LightSwitch_ApplyMonitorSettingsExpander`, add a loading card using the shared Settings UI resource:

```xml
<tkcontrols:SettingsCard
    HorizontalContentAlignment="Center"
    Visibility="{x:Bind ViewModel.IsProfilesLoading, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <ProgressRing
            x:Uid="PowerDisplay_ProfilesLoadingIndicator"
            Width="24"
            Height="24"
            IsActive="True" />
        <TextBlock
            x:Uid="PowerDisplay_ProfilesLoadingText"
            VerticalAlignment="Center" />
    </StackPanel>
</tkcontrols:SettingsCard>
```

Bind both existing profile `SettingsCard.Visibility` values through `ReverseBoolToVisibilityConverter` and include `!IsProfilesLoading` in their enabled state via a new ViewModel property:

```csharp
public bool CanSelectPowerDisplayProfile => IsPowerDisplayEnabled && !IsProfilesLoading;
```

Raise `CanSelectPowerDisplayProfile` when either dependency changes.

- [ ] **Step 6: Build and run all focused tests**

Run:

```powershell
Set-Location src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' `
  '.\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  '/TestCaseFilter:FullyQualifiedName~PowerDisplayProfilesTests|FullyQualifiedName~ProfileStoreTests|FullyQualifiedName~ProfileOperationCoordinatorTests|FullyQualifiedName~LightSwitchProfileResolverTests|FullyQualifiedName~LightSwitchProfileSettingsUpdaterTests'

Set-Location ..\..\..\settings-ui\Settings.UI
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

Expected: all focused tests pass and Settings UI XAML compilation exits 0.

- [ ] **Step 7: Commit**

```powershell
git add -- `
  src\settings-ui\Settings.UI\ViewModels\LightSwitchViewModel.cs `
  src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml `
  src\settings-ui\Settings.UI\SettingsXAML\Views\LightSwitchPage.xaml.cs
git commit -m "fix(settings-ui): load LightSwitch profiles asynchronously" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 0ad959b9-dadf-48f6-9367-a6738da252c4"
```

---

### Task 6: Final Integration Validation

**Files:**
- Verify all files changed in Tasks 1-5.
- No new file is created in this task.

**Interfaces:**
- Consumes all interfaces produced by Tasks 1-5.
- Produces a verified branch ready for review.

- [ ] **Step 1: Verify no forbidden synchronous UI profile transactions remain**

Run:

```powershell
rg -n "Profile(Service|Helper)\\.(LoadProfiles|LoadProfilesEnsuringIds|AddOrUpdateProfile|RemoveProfileById|UpdateProfiles)\\(" `
  src\modules\powerdisplay\PowerDisplay `
  src\settings-ui\Settings.UI
```

Expected: remaining synchronous calls are confined to background-only code. Every UI entry point uses an `Async` method or starts inside a worker task.

- [ ] **Step 2: Verify PowerDisplay no longer writes LightSwitch settings**

Run:

```powershell
rg -n "GetSettingsOrDefault<LightSwitchSettings>|SaveSettings\\(.*LightSwitch|ReconcileAndSend" `
  src\modules\powerdisplay\PowerDisplay
```

Expected: no `GetSettingsOrDefault<LightSwitchSettings>`, direct LightSwitch `SaveSettings`, or `ReconcileAndSend` call exists in the PowerDisplay process.

- [ ] **Step 3: Verify all loading surfaces and resources**

Run:

```powershell
rg -n "IsProfilesLoading|ProfilesLoading" `
  src\modules\powerdisplay\PowerDisplay `
  src\settings-ui\Settings.UI
```

Expected: matches exist in all three ViewModels/XAML surfaces and in both English resource files.

- [ ] **Step 4: Build the affected projects**

Run in order, waiting for exit code 0 after each:

```powershell
Set-Location (git rev-parse --show-toplevel)
Set-Location src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug

Set-Location ..\PowerDisplay
& ..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug

Set-Location ..\..\..\settings-ui\Settings.UI.Library
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug

Set-Location ..\Settings.UI
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug
```

Expected: every command exits 0. If a build fails, inspect the adjacent `build.debug.x64.errors.log` before continuing.

- [ ] **Step 5: Run the complete PowerDisplay test assembly**

Run:

```powershell
Set-Location (git rev-parse --show-toplevel)
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' `
  'src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64
```

Expected: all profile-id, resolver, updater, coordinator, and store tests pass. If unchanged CrashRecovery tests fail with `REGDB_E_CLASSNOTREG`, record them as local WinRT registration failures and rerun the focused filter from Task 5 to prove the changed scope is green.

- [ ] **Step 6: Run diff hygiene checks**

Run:

```powershell
git --no-pager diff --check origin/main...HEAD
git --no-pager status --short
```

Expected: no whitespace errors and only intended source, test, XAML, resource, spec, and plan files are present.

- [ ] **Step 7: Review the cumulative diff**

Run:

```powershell
git --no-pager diff --stat origin/main...HEAD
git --no-pager diff --name-only origin/main...HEAD
```

Confirm:

- No direct LightSwitch settings write remains in PowerDisplay.
- Empty successful loads reconcile stale references.
- Failed loads do not reconcile.
- All three loading surfaces are localized and accessible.
- Every UI-originated profile transaction is asynchronous.
