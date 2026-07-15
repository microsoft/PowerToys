# PowerDisplay Profile ID Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the approved PR #49175 cleanup, characterize the existing LightSwitch settings write, and leave the accepted migration, concurrency, and native event-gating trade-offs unchanged.

**Architecture:** Keep the current profile migration and LightSwitch full-document write. Add characterization coverage for the complete known LightSwitch schema, tighten the profile API around stable IDs, localize the shared profile label inside `PowerDisplay.Models`, and make the profile semaphore cleanup exception-safe. Documentation and the PR body are updated only after code and test counts are final.

**Tech Stack:** C#/.NET, MSTest, WinUI 3 XAML, C++ PowerToys module interface, `.resx` resources, PowerShell, `gh` CLI.

## Global Constraints

- Do not change the dependency on initial monitor discovery for profile ID migration.
- Do not redesign or serialize access to `LightSwitch/settings.json`.
- Do not add reverse IPC from PowerDisplay to Runner.
- Do not fix the native LightSwitch ID event-gating defect in this change.
- Do not change the existing behavior for unmatched legacy monitor IDs.
- Do not add third-party dependencies.
- Keep code, comments, tests, and documentation in English.
- Use repository build scripts; do not use `dotnet test`.
- Build affected projects for `x64` / `Debug`.
- Include the required Copilot commit trailers in every implementation commit.

---

## File Structure

**Create**

- `src/modules/powerdisplay/PowerDisplay.Models/Properties/Resources.resx` — shared localized profile display format.
- `src/modules/powerdisplay/PowerDisplay.Models/ProfileDisplayNameFormatter.cs` — resource loading, formatting, diagnostics, and neutral fallback.

**Modify**

- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/LightSwitchProfileIdTests.cs` — complete LightSwitch settings serialization characterization.
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplayProfilesTests.cs` — legacy lookup and localized formatter tests.
- `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayProfiles.cs` — explicit legacy name lookup.
- `src/settings-ui/Settings.UI.Library/LightSwitchProfileReferenceHelper.cs` — use the explicit legacy lookup.
- `src/modules/powerdisplay/PowerDisplay.Models/ProfileHelper.cs` — remove unused synchronous wrappers.
- `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayProfile.cs` — delegate display formatting to the resource-backed helper.
- `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs` — guarantee semaphore release.
- `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp` — ID terminology cleanup.
- `doc/devdocs/modules/powerdisplay/design.md` — current schema, duplicate-name behavior, APIs, and test flow.
- GitHub PR #49175 body — current APIs, tests, and accepted behavior.

---

### Task 1: Characterize Complete LightSwitch Settings Serialization

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/LightSwitchProfileIdTests.cs`

**Interfaces:**
- Consumes: `LightSwitchSettings.ToJsonString()` and `SettingsSerializationContext.Default.LightSwitchSettings`.
- Produces: A characterization test proving all known LightSwitch fields survive the migration serialization path.

- [ ] **Step 1: Add the complete round-trip test**

Add this test to `LightSwitchProfileIdTests`:

```csharp
[TestMethod]
public void LightSwitchSettings_ToJsonString_RoundTripsAllKnownProperties()
{
    var settings = new LightSwitchSettings();
    settings.Properties.ChangeSystem.Value = false;
    settings.Properties.ChangeApps.Value = false;
    settings.Properties.ScheduleMode.Value = "SunsetToSunrise";
    settings.Properties.LightTime.Value = 451;
    settings.Properties.DarkTime.Value = 1217;
    settings.Properties.SunriseOffset.Value = -15;
    settings.Properties.SunsetOffset.Value = 20;
    settings.Properties.Latitude.Value = "47.642";
    settings.Properties.Longitude.Value = "-122.136";
    settings.Properties.ToggleThemeHotkey.Value = new HotkeySettings(
        win: false,
        ctrl: true,
        alt: true,
        shift: false,
        code: 0x4C);
    settings.Properties.EnableDarkModeProfile.Value = true;
    settings.Properties.EnableLightModeProfile.Value = true;
    settings.Properties.DarkModeProfile.Value = "Night";
    settings.Properties.LightModeProfile.Value = "Day";
    settings.Properties.DarkModeProfileId.Value = 7;
    settings.Properties.LightModeProfileId.Value = 3;

    var json = settings.ToJsonString();
    var roundTripped = JsonSerializer.Deserialize(
        json,
        SettingsSerializationContext.Default.LightSwitchSettings);

    Assert.IsNotNull(roundTripped);
    Assert.AreEqual(settings.Name, roundTripped.Name);
    Assert.AreEqual(settings.Version, roundTripped.Version);
    Assert.IsFalse(roundTripped.Properties.ChangeSystem.Value);
    Assert.IsFalse(roundTripped.Properties.ChangeApps.Value);
    Assert.AreEqual("SunsetToSunrise", roundTripped.Properties.ScheduleMode.Value);
    Assert.AreEqual(451, roundTripped.Properties.LightTime.Value);
    Assert.AreEqual(1217, roundTripped.Properties.DarkTime.Value);
    Assert.AreEqual(-15, roundTripped.Properties.SunriseOffset.Value);
    Assert.AreEqual(20, roundTripped.Properties.SunsetOffset.Value);
    Assert.AreEqual("47.642", roundTripped.Properties.Latitude.Value);
    Assert.AreEqual("-122.136", roundTripped.Properties.Longitude.Value);
    Assert.IsFalse(roundTripped.Properties.ToggleThemeHotkey.Value.Win);
    Assert.IsTrue(roundTripped.Properties.ToggleThemeHotkey.Value.Ctrl);
    Assert.IsTrue(roundTripped.Properties.ToggleThemeHotkey.Value.Alt);
    Assert.IsFalse(roundTripped.Properties.ToggleThemeHotkey.Value.Shift);
    Assert.AreEqual(0x4C, roundTripped.Properties.ToggleThemeHotkey.Value.Code);
    Assert.IsTrue(roundTripped.Properties.EnableDarkModeProfile.Value);
    Assert.IsTrue(roundTripped.Properties.EnableLightModeProfile.Value);
    Assert.AreEqual("Night", roundTripped.Properties.DarkModeProfile.Value);
    Assert.AreEqual("Day", roundTripped.Properties.LightModeProfile.Value);
    Assert.AreEqual(7, roundTripped.Properties.DarkModeProfileId.Value);
    Assert.AreEqual(3, roundTripped.Properties.LightModeProfileId.Value);
}
```

- [ ] **Step 2: Build the test project**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
```

Expected: exit code `0`.

- [ ] **Step 3: Run the characterization test**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe' `
  '.\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~LightSwitchSettings_ToJsonString_RoundTripsAllKnownProperties'
```

Expected: `1` passed, `0` failed. This is a characterization test and should pass before production changes.

- [ ] **Step 4: Commit the characterization test**

```powershell
git add -- 'src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\LightSwitchProfileIdTests.cs'
git commit -m 'test: cover complete LightSwitch settings serialization' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

---

### Task 2: Make Legacy Name Lookup Explicit and Remove Sync Wrappers

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplayProfilesTests.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayProfiles.cs`
- Modify: `src/settings-ui/Settings.UI.Library/LightSwitchProfileReferenceHelper.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay.Models/ProfileHelper.cs`

**Interfaces:**
- Consumes: `PowerDisplayProfiles.Profiles`, `ProfileHelper` asynchronous persistence methods.
- Produces: `PowerDisplayProfile? GetLegacyProfileByName(string name)` and an async-only public persistence surface.

- [ ] **Step 1: Write the failing legacy lookup test**

Add this test to `PowerDisplayProfilesTests`:

```csharp
[TestMethod]
public void GetLegacyProfileByName_ReturnsFirstCaseInsensitiveMatch()
{
    var profiles = new PowerDisplayProfiles();
    var first = MakeProfile("Same", id: 1);
    var second = MakeProfile("same", id: 2);
    profiles.Profiles.Add(first);
    profiles.Profiles.Add(second);

    Assert.AreSame(first, profiles.GetLegacyProfileByName("SAME"));
}
```

- [ ] **Step 2: Build to verify the new API test fails**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
```

Expected: non-zero exit with a compiler error that `PowerDisplayProfiles` has no `GetLegacyProfileByName` definition.

- [ ] **Step 3: Rename the lookup and clarify its contract**

Replace `GetProfile` in `PowerDisplayProfiles.cs` with:

```csharp
/// <summary>
/// Gets the first profile whose name matches a pre-ID persisted reference.
/// This lookup is only for legacy migration because profile names are not unique.
/// </summary>
public PowerDisplayProfile? GetLegacyProfileByName(string name)
{
    return Profiles.FirstOrDefault(
        profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
```

Update `LightSwitchProfileReferenceHelper.ReconcileOne`:

```csharp
var profile = profiles.GetLegacyProfileByName(originalName);
```

- [ ] **Step 4: Remove unused synchronous persistence wrappers**

Delete these methods and their XML documentation from `ProfileHelper.cs`:

```csharp
public static bool AddOrUpdateProfile(PowerDisplayProfile profile)
{
    if (profile == null || !profile.IsValid())
    {
        return false;
    }

    _profileStore.Value.AddOrUpdateProfile(profile);
    return true;
}

public static bool RemoveProfileById(int id)
{
    return _profileStore.Value.RemoveProfileById(id);
}
```

Keep:

```csharp
public static Task AddOrUpdateProfileAsync(
    PowerDisplayProfile profile,
    CancellationToken cancellationToken = default)
    => _profileStore.Value.AddOrUpdateProfileAsync(profile, cancellationToken);

public static Task<bool> RemoveProfileByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
    => _profileStore.Value.RemoveProfileByIdAsync(id, cancellationToken);
```

- [ ] **Step 5: Verify no production caller uses the removed or ambiguous APIs**

Run:

```powershell
rg 'ProfileHelper\.(AddOrUpdateProfile|RemoveProfileById)\(' .\src
rg '\.GetProfile\(' `
  .\src\modules\powerdisplay\PowerDisplay.Models `
  .\src\settings-ui\Settings.UI.Library
```

Expected: both commands return no matches.

- [ ] **Step 6: Build and run the focused tests**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests

& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe' `
  '.\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~PowerDisplayProfilesTests|FullyQualifiedName~LightSwitchProfileReferenceHelperTests'
```

Expected: build exit code `0`; all selected tests pass.

- [ ] **Step 7: Commit the API cleanup**

```powershell
git add -- `
  'src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\PowerDisplayProfilesTests.cs' `
  'src\modules\powerdisplay\PowerDisplay.Models\PowerDisplayProfiles.cs' `
  'src\modules\powerdisplay\PowerDisplay.Models\ProfileHelper.cs' `
  'src\settings-ui\Settings.UI.Library\LightSwitchProfileReferenceHelper.cs'
git commit -m 'refactor(powerdisplay): clarify stable profile APIs' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

---

### Task 3: Localize the Shared Profile Display Label

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/Properties/Resources.resx`
- Create: `src/modules/powerdisplay/PowerDisplay.Models/ProfileDisplayNameFormatter.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayProfile.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplayProfilesTests.cs`

**Interfaces:**
- Consumes: `ResourceManager`, `CultureInfo.CurrentCulture`, `PowerDisplayProfile.Name`, and `PowerDisplayProfile.Id`.
- Produces: `ProfileDisplayNameFormatter.Format(string name, int id)` and the existing `PowerDisplayProfile.DisplayName` property.

- [ ] **Step 1: Write failing formatter tests**

Add these tests to `PowerDisplayProfilesTests`:

```csharp
[TestMethod]
public void ProfileDisplayNameFormatter_UsesProvidedFormatOrder()
{
    Assert.AreEqual(
        "#4: Gaming",
        ProfileDisplayNameFormatter.Format("Gaming", 4, "#{1}: {0}"));
}

[TestMethod]
public void ProfileDisplayNameFormatter_InvalidFormat_FallsBackToNeutral()
{
    Assert.AreEqual(
        "Gaming (#4)",
        ProfileDisplayNameFormatter.Format("Gaming", 4, "{0"));
}
```

- [ ] **Step 2: Build to verify the formatter tests fail**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
```

Expected: non-zero exit because `ProfileDisplayNameFormatter` does not exist.

- [ ] **Step 3: Add the shared resource**

Create `PowerDisplay.Models/Properties/Resources.resx` with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <data name="ProfileDisplayNameFormat" xml:space="preserve">
    <value>{0} (#{1})</value>
    <comment>{0} is the profile name. {1} is the stable profile ID.</comment>
  </data>
</root>
```

- [ ] **Step 4: Add the formatter**

Create `ProfileDisplayNameFormatter.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Resources;

namespace PowerDisplay.Models
{
    internal static class ProfileDisplayNameFormatter
    {
        private const string NeutralFormat = "{0} (#{1})";
        private const string ResourceName = "ProfileDisplayNameFormat";

        private static readonly ResourceManager ResourceManager = new(
            "PowerDisplay.Models.Properties.Resources",
            typeof(ProfileDisplayNameFormatter).Assembly);

        public static string Format(string name, int id)
        {
            var format = ResourceManager.GetString(
                ResourceName,
                CultureInfo.CurrentUICulture);
            return Format(name, id, format);
        }

        internal static string Format(string name, int id, string? format)
        {
            var selectedFormat = string.IsNullOrEmpty(format)
                ? NeutralFormat
                : format;

            try
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    selectedFormat,
                    name,
                    id);
            }
            catch (FormatException ex)
            {
                Trace.TraceError(
                    $"Invalid {ResourceName} resource: {ex.Message}");
                return string.Format(
                    CultureInfo.CurrentCulture,
                    NeutralFormat,
                    name,
                    id);
            }
        }
    }
}
```

- [ ] **Step 5: Route the model property through the formatter**

Change `PowerDisplayProfile.DisplayName` to:

```csharp
[JsonIgnore]
public string DisplayName => ProfileDisplayNameFormatter.Format(Name, Id);
```

- [ ] **Step 6: Build and run the model tests**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests

& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe' `
  '.\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64 `
  /TestCaseFilter:'FullyQualifiedName~PowerDisplayProfilesTests'
```

Expected: build exit code `0`; all `PowerDisplayProfilesTests` pass, including the two formatter tests and the existing neutral-label test.

- [ ] **Step 7: Commit the localization cleanup**

```powershell
git add -- `
  'src\modules\powerdisplay\PowerDisplay.Models\Properties\Resources.resx' `
  'src\modules\powerdisplay\PowerDisplay.Models\ProfileDisplayNameFormatter.cs' `
  'src\modules\powerdisplay\PowerDisplay.Models\PowerDisplayProfile.cs' `
  'src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\PowerDisplayProfilesTests.cs'
git commit -m 'fix(powerdisplay): localize profile display labels' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

---

### Task 4: Guarantee Profile Semaphore Release

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs:497-512`

**Interfaces:**
- Consumes: `_profileOperationGate`, `IsProfilesLoading`, and the existing operation delegate.
- Produces: The same `RunProfileOperationAsync` signature with exception-safe release.

- [ ] **Step 1: Run a structural check that fails before the change**

Run:

```powershell
$source = Get-Content `
  '.\src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.cs' `
  -Raw

if ($source -notmatch 'finally\s*\{\s*try\s*\{\s*IsProfilesLoading = false;\s*\}\s*finally\s*\{\s*_profileOperationGate\.Release\(\);') {
  throw 'Nested finally release pattern is missing.'
}
```

Expected: command throws `Nested finally release pattern is missing.`

- [ ] **Step 2: Implement the nested finally**

Replace the current `finally` block with:

```csharp
finally
{
    try
    {
        IsProfilesLoading = false;
    }
    finally
    {
        _profileOperationGate.Release();
    }
}
```

- [ ] **Step 3: Re-run the structural check**

Run the Step 1 PowerShell command again.

Expected: exit code `0` with no output.

- [ ] **Step 4: Build the PowerDisplay app**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay
```

Expected: exit code `0`.

- [ ] **Step 5: Commit the semaphore cleanup**

```powershell
git add -- 'src\modules\powerdisplay\PowerDisplay\ViewModels\MainViewModel.cs'
git commit -m 'fix(powerdisplay): always release profile operation gate' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

---

### Task 5: Correct Native ApplyProfile Terminology

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp:373-382`

**Interfaces:**
- Consumes: `action_object.get_value()` and `TrySendMessage`.
- Produces: No runtime change; only ID-accurate local names, comments, and logging.

- [ ] **Step 1: Confirm stale terminology exists**

Run:

```powershell
rg 'profileName|profile name' `
  '.\src\modules\powerdisplay\PowerDisplayModuleInterface\dllmain.cpp'
```

Expected: matches in the `ApplyProfile` custom action branch.

- [ ] **Step 2: Rename the local and update comments/logging**

Replace the branch body with:

```cpp
Logger::trace(L"ApplyProfile action received");

// Get the profile ID from the action value.
std::wstring profileId = action_object.get_value();
Logger::trace(L"ApplyProfile: profile ID = '{}'", profileId);

// Send the ApplyProfile message with the profile ID via Named Pipe.
TrySendMessage(CommonSharedConstants::POWER_DISPLAY_APPLY_PROFILE_MESSAGE, profileId, L"ApplyProfile action");
```

- [ ] **Step 3: Confirm stale terminology is gone**

Run:

```powershell
rg 'profileName|profile name' `
  '.\src\modules\powerdisplay\PowerDisplayModuleInterface\dllmain.cpp'
```

Expected: no matches.

- [ ] **Step 4: Build the module interface**

Run:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 `
  -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplayModuleInterface
```

Expected: exit code `0`.

- [ ] **Step 5: Commit the terminology cleanup**

```powershell
git add -- 'src\modules\powerdisplay\PowerDisplayModuleInterface\dllmain.cpp'
git commit -m 'chore(powerdisplay): name profile ID payload consistently' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

---

### Task 6: Refresh Developer Documentation and PR Description

**Files:**
- Modify: `doc/devdocs/modules/powerdisplay/design.md`
- External: GitHub PR #49175 body

**Interfaces:**
- Consumes: Final code/API names and final test count from Tasks 1-5.
- Produces: Accurate developer documentation and PR metadata.

- [ ] **Step 1: Update the LightSwitch JSON example**

Replace the profile-related JSON example with:

```json
{
  "properties": {
    "enableLightModeProfile": { "value": true },
    "lightModeProfile": { "value": "" },
    "lightModeProfileId": { "value": 3 },
    "enableDarkModeProfile": { "value": true },
    "darkModeProfile": { "value": "" },
    "darkModeProfileId": { "value": 7 }
  }
}
```

Add one sentence immediately below it:

```md
The name fields are retained only for migration from pre-ID settings; current code persists and resolves the positive ID fields.
```

- [ ] **Step 2: Correct the profile creation sequence**

Replace:

```md
Note over ProfileDialog: Check name unique,<br/>at least one monitor selected
```

with:

```md
Note over ProfileDialog: Check non-empty name,<br/>at least one monitor selected
```

Ensure the sequence names `ProfileHelper.AddOrUpdateProfileAsync`, `ProfileStore`, the named mutex, and atomic replacement; do not reintroduce `ProfileService` or `LoadProfilesEnsuringIds`.

- [ ] **Step 3: Verify stale documentation terms are gone**

Run:

```powershell
rg 'IProfileService|ProfileService|LoadProfilesEnsuringIds|Check name unique|light_mode_profile|dark_mode_profile' `
  '.\doc\devdocs\modules\powerdisplay\design.md'
```

Expected: no matches.

- [ ] **Step 4: Commit the repository documentation**

```powershell
git add -- 'doc\devdocs\modules\powerdisplay\design.md'
git commit -m 'docs: refresh PowerDisplay profile ID design' `
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: ae39664b-7f6e-4f96-9a6d-c88f98a4070a"
```

- [ ] **Step 5: Replace the PR body with current content**

Create a temporary body and update the PR:

```powershell
$bodyPath = Join-Path $env:TEMP 'pr-49175-body.md'

@'
## Summary of the Pull Request

Gives every saved PowerDisplay profile a stable, auto-incrementing integer ID and makes the app address profiles by that ID instead of by name. Duplicate profile names are allowed, renames preserve identity, and LightSwitch stores stable profile references.

> Split out of the PowerDisplay CLI branch (#48632). CLI-specific contracts and commands remain in that stacked PR.

## PR Checklist

- [x] **Closes:** N/A - split from #48632.
- [x] **Communication:** Discussed with core contributors.
- [x] **Tests:** Added and passing in `PowerDisplay.Lib.UnitTests`.
- [x] **Localization:** The composed profile label uses a shared localized format resource.
- [x] **New binaries:** None.
- [x] **Documentation updated:** `doc/devdocs/modules/powerdisplay/design.md`.

## Implementation

### Profile model and persistence

- `PowerDisplayProfile.Id` is the stable JSON `id`; `0` means unassigned.
- `PowerDisplayProfiles.NextId` is monotonic and IDs are never reused.
- `SetProfile` assigns IDs to new profiles and replaces existing profiles by ID.
- Duplicate names are supported; name lookup remains only for migration of legacy references.
- `ProfileStore` serializes cross-process load/modify/save operations with a named mutex and atomically replaces `profiles.json`.
- Production callers use asynchronous `ProfileHelper` APIs.

### Migration and application

- Initial PowerDisplay discovery assigns missing profile IDs and migrates legacy monitor IDs.
- LightSwitch legacy name references are reconciled to IDs and written back to the current typed settings schema.
- Settings UI and Named Pipe ApplyProfile actions send invariant positive profile IDs.
- PowerDisplay validates the ID, loads the current profile, and applies its monitor settings.

### Settings UI

- Create, edit, apply, and delete operations use stable IDs.
- LightSwitch selectors store profile IDs and keep legacy name fields only for migration.
- Profile lists use a localized name-and-ID label so duplicate names remain distinguishable.

## Accepted Trade-offs

- Profile ID migration remains dependent on the initial monitor discovery; a failed or delayed discovery can temporarily hide legacy ID-less profiles.
- The one-time PowerDisplay LightSwitch migration rewrites the complete current typed settings object and does not add a new cross-process settings transaction.
- Native LightSwitch ID event gating is tracked separately and is not addressed by this cleanup.

## Validation

- Built the affected x64 Debug projects with the repository build scripts.
- `PowerDisplay.Lib.UnitTests`: 184 passed, 0 failed.
'@ | Set-Content -LiteralPath $bodyPath -Encoding utf8

gh pr edit 49175 --body-file $bodyPath
Remove-Item -LiteralPath $bodyPath
```

Expected: `gh pr edit` succeeds.

- [ ] **Step 6: Verify the PR body**

Run:

```powershell
$body = (gh pr view 49175 --json body | ConvertFrom-Json).body
$required = @(
  'stable, auto-incrementing integer ID',
  'Duplicate profile names are allowed',
  '184 passed, 0 failed',
  'Accepted Trade-offs')
$forbidden = @(
  'LoadProfilesEnsuringIds',
  'LightSwitchProfileResolverTests',
  '164/164')

foreach ($text in $required) {
  if (-not $body.Contains($text)) {
    throw "Missing PR body text: $text"
  }
}

foreach ($text in $forbidden) {
  if ($body.Contains($text)) {
    throw "Stale PR body text remains: $text"
  }
}
```

Expected: exit code `0`.

---

### Task 7: Full Validation

**Files:**
- Verify all files changed in Tasks 1-6.

**Interfaces:**
- Consumes: All task deliverables.
- Produces: A clean, buildable, tested local branch and current PR metadata.

- [ ] **Step 1: Build every affected project**

Run each command and require exit code `0`:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplayModuleInterface

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 -Configuration Debug `
  -Path .\src\settings-ui\Settings.UI

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build.ps1 `
  -Platform x64 -Configuration Debug `
  -Path .\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests
```

- [ ] **Step 2: Run the complete PowerDisplay test assembly**

Run:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe' `
  '.\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll' `
  /Platform:x64
```

Expected:

```text
Test Run Successful.
Total tests: 184
Passed: 184
```

- [ ] **Step 3: Run final repository checks**

```powershell
git --no-pager diff --check
git --no-pager status --short
rg 'ProfileHelper\.(AddOrUpdateProfile|RemoveProfileById)\(' .\src
rg 'profileName|profile name' `
  '.\src\modules\powerdisplay\PowerDisplayModuleInterface\dllmain.cpp'
```

Expected:

- `git diff --check` exits `0`.
- `git status --short` is empty.
- Both `rg` commands return no matches.

- [ ] **Step 4: Confirm the PR head and body**

```powershell
$pr = gh pr view 49175 --json headRefOid,body,url | ConvertFrom-Json
$local = git rev-parse HEAD

if ($pr.headRefOid -ne $local) {
  Write-Warning 'Local commits have not been pushed; do not claim the remote PR contains the cleanup yet.'
}

if (-not $pr.body.Contains('184 passed, 0 failed')) {
  throw 'PR validation count is stale.'
}

$pr.url
```

Expected: the body check passes. If the local branch is ahead, report that push is still required rather than claiming the remote diff is updated.
