# PowerDisplay Profile ID Cleanup Design

## Context

PR #49175 adds stable integer IDs to PowerDisplay profiles and migrates LightSwitch profile references from names to IDs. The review identified several cleanup opportunities and two architectural risks. The accepted scope is to complete the cleanup work, verify the existing LightSwitch settings read/write path, and leave the accepted migration-order and concurrent-write trade-offs unchanged. The native LightSwitch ID notification defect will be handled separately.

## Goals

- Make profile-operation semaphore release exception-safe.
- Remove or clarify APIs that conflict with the stable-ID contract.
- Correct stale native IPC terminology.
- Localize the composed profile display label without introducing duplicate UI wrappers.
- Verify that the current LightSwitch settings serialization preserves the complete known schema.
- Update developer documentation and the PR description to match the final implementation.

## Non-Goals

- Do not change the dependency on initial monitor discovery for profile ID migration.
- Do not redesign or serialize access to `LightSwitch/settings.json`.
- Do not add reverse IPC from PowerDisplay to Runner.
- Do not fix the native LightSwitch ID event-gating defect in this change.
- Do not change the existing behavior for unmatched legacy monitor IDs.

## Verified LightSwitch Settings Flow

The existing C# read/write flow is structurally correct for the current schema:

1. `LightSwitchSettings.ModuleName` is `"LightSwitch"`, matching the native settings path.
2. `SettingsUtils.GetSettingsOrDefault<LightSwitchSettings>` reads the complete typed settings object.
3. `LightSwitchSettings` is registered in `SettingsSerializationContext`, so `ToJsonString()` uses the AOT-compatible generated metadata.
4. `LightSwitchSettings.Clone()` copies both profile ID fields, both legacy name fields, and all unrelated settings fields.
5. `SettingsUtils.SaveSettings` writes the serialized object to the LightSwitch settings path.
6. Native LightSwitch watches that file and reloads it after the write stabilizes.

The accepted risks remain:

- The full-document write can overwrite a concurrent update from another process.
- `SettingsUtils.SaveSettings` logs and suppresses most write failures.
- Native LightSwitch does not yet parse the new ID fields.

This cleanup will add a complete serialization round-trip test so accidental field omission is detected.

## Design

### 1. Exception-Safe Profile Operation Gate

`MainViewModel.RunProfileOperationAsync` will keep the loading-state reset before releasing the semaphore, but the release will move into a nested `finally`:

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

This preserves current UI ordering while guaranteeing that a throwing `PropertyChanged` subscriber cannot leak the semaphore.

### 2. Stable-ID API Cleanup

- Rename `PowerDisplayProfiles.GetProfile(string name)` to `GetLegacyProfileByName(string name)`.
- Keep the method public because `Settings.UI.Library` performs legacy reconciliation from a separate assembly.
- Update the XML documentation to state that the lookup is only for migration of pre-ID references and returns the first match.
- Remove the unused synchronous `ProfileHelper.AddOrUpdateProfile` and `ProfileHelper.RemoveProfileById` wrappers.
- Keep the asynchronous wrappers as the supported production API.

This makes ambiguous name lookup explicit and prevents future UI code from selecting the synchronous persistence path.

### 3. Native IPC Terminology

In `PowerDisplayModuleInterface/dllmain.cpp`, rename the local `profileName` variable to `profileId` and update the associated comments and log message. The payload and behavior remain unchanged.

### 4. Localized Profile Display Label

The profile label is shared by PowerDisplay and Settings UI, both of which bind directly to `PowerDisplayProfile`. To avoid introducing separate wrapper collections and converters in both processes:

- Add `PowerDisplay.Models/Properties/Resources.resx`.
- Add a `ProfileDisplayNameFormat` resource with the neutral value `{0} (#{1})`.
- Include a translator comment documenting `{0}` as the profile name and `{1}` as the stable profile ID.
- Add an internal `ProfileDisplayNameFormatter` that loads the resource through `ResourceManager` and formats it with `CultureInfo.CurrentCulture`.
- Keep `PowerDisplayProfile.DisplayName`, but delegate formatting to that helper.
- Catch only `FormatException`, report the malformed localized format with `Trace.TraceError`, and fall back to the neutral constant `{0} (#{1})`.
- Update unit tests to validate placeholder substitution rather than a hard-coded implementation detail.

The shared resource keeps all existing XAML bindings intact and provides one translation source for both UI processes.

### 5. LightSwitch Serialization Coverage

Extend `LightSwitchProfileIdTests` with a full `LightSwitchSettings.ToJsonString()` round-trip test. The test will set representative values for:

- Schedule mode, times, coordinates, and offsets.
- Theme target flags and toggle hotkey.
- Profile enable flags.
- Legacy profile names.
- Stable profile IDs.

The serialized JSON will be deserialized through the registered source-generation context, and every value will be asserted. This verifies that the migration write preserves all known current fields. It does not attempt to solve concurrent writers or unknown future JSON extensions.

### 6. Documentation and PR Description

Update `doc/devdocs/modules/powerdisplay/design.md` to:

- Show `darkModeProfileId` and `lightModeProfileId` in the LightSwitch JSON example.
- Mark the name fields as legacy migration fields.
- Remove the unique-profile-name requirement.
- Describe the current `ProfileHelper`/`ProfileStore` asynchronous APIs.
- Reflect the current test and migration behavior.

Update PR #49175 through `gh pr edit` to remove deleted API and test names, correct the current validation count, and accurately describe the accepted migration behavior.

## Error Handling

- Semaphore release must occur regardless of loading-state notification failures.
- The LightSwitch write path retains its current logging and failure behavior.
- Resource formatting will fall back only for a missing resource or `FormatException`, and malformed formats will be reported through `Trace.TraceError`.
- No broad exception handling will be added.

## Testing

- No new automated test will be added for the semaphore cleanup because the repository has no test project that constructs the WinUI `MainViewModel`; the nested-`finally` invariant will be verified by source review and the affected app build.
- Update model tests for `GetLegacyProfileByName`.
- Remove tests that depend on deleted synchronous `ProfileHelper` wrappers, if any.
- Add the complete LightSwitch settings serialization round-trip test.
- Update `DisplayName` tests for resource-backed formatting.
- Build `PowerDisplay.Lib`, `PowerDisplay`, `Settings.UI`, and `PowerDisplay.Lib.UnitTests` for x64 Debug.
- Run the complete `PowerDisplay.Lib.UnitTests` assembly.
- Run `git diff --check`.

## Acceptance Criteria

- No production caller references the removed synchronous profile helper methods.
- All name-based profile lookups are explicitly marked as legacy migration behavior.
- `_profileOperationGate` cannot remain acquired if loading-state notification throws.
- All existing profile UI surfaces continue to show the same neutral English label.
- The label format is resource-backed and translation-ready.
- A complete LightSwitch settings object round-trips without losing any known field.
- `design.md` and the GitHub PR description match the current code.
- The accepted migration-order, concurrent-write, and native event-gating behaviors are unchanged.
