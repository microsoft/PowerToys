# PowerDisplay Monitor Blacklist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a model-level (EdidId-based) monitor blacklist to PowerDisplay that filters monitors out at the discovery stage — before any DDC/CI or WMI probing — sourced from a built-in JSON shipped with PowerToys plus a user-editable custom list under Settings → Advanced.

**Architecture:** A new `MonitorBlacklistEntry` shared record lives in `PowerDisplay.Models`. A built-in JSON (initially empty) is embedded into that assembly; a tiny loader exposes the entries via a static cached list. A `MonitorBlacklistService` in `PowerDisplay.Lib` holds the union of built-in ∪ custom and answers `IsBlocked(monitorId)` via `MonitorIdentity.EdidIdFromMonitorId`. The service is injected into `MonitorManager` (just like `SetMaxCompatibilityMode`) before every discovery and filters the QueryDisplayConfig inventory before any controller dispatch. Custom entries live as a new field on `PowerDisplayProperties`, and a new SettingsExpander under the existing "Advanced settings" group on the PowerDisplay settings page lets users add / edit / delete entries via a `ContentDialog`.

**Tech Stack:** C# 12, .NET 9 (Native AOT compatible), MSTest, WinUI 3 (CommunityToolkit Settings controls), `System.Text.Json` source-generated serialization.

**Reference spec:** [docs/superpowers/specs/2026-05-21-powerdisplay-monitor-blacklist-design.md](../specs/2026-05-21-powerdisplay-monitor-blacklist-design.md)

---

## File Structure

**New files**

| Path | Responsibility |
| --- | --- |
| `src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistEntry.cs` | Shared POCO for one blacklist entry (`EdidId` + `Comments`) |
| `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklistFile.cs` | DTO wrapping the JSON file shape (`version` + `entries`) |
| `src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistSerializationContext.cs` | `JsonSerializerContext` for AOT-safe deserialization |
| `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.cs` | Static loader; cached `IReadOnlyList<MonitorBlacklistEntry>` via `Lazy<>` |
| `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.json` | The data file (initially empty `entries: []`), embedded resource |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorBlacklistService.cs` | Union HashSet + `IsBlocked(monitorId)` |
| `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/BuiltInMonitorBlacklistTests.cs` | Loader tests (presence, normalization, malformed-input safety) |
| `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorBlacklistServiceTests.cs` | Service tests (matching, case-insensitivity, false-positive guards) |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml` | Add/edit dialog markup |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml.cs` | Dialog code-behind (live normalization, validation) |

**Modified files**

| Path | Change |
| --- | --- |
| `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplay.Models.csproj` | Add `<EmbeddedResource>` entry for the JSON |
| `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs` | New `MonitorBlacklist` field |
| `src/modules/powerdisplay/PowerDisplay\Helpers\MonitorManager.cs` | `_blacklistService` field + `SetMonitorBlacklist(...)` + filter `inventory` before dispatch |
| `src/modules/powerdisplay/PowerDisplay\ViewModels\MainViewModel.Monitors.cs` | Call `SetMonitorBlacklist(...)` before each `DiscoverMonitorsAsync` (mirrors `SetMaxCompatibilityMode`) |
| `src/modules/powerdisplay/PowerDisplay\Telemetry\Events\PowerDisplaySettingsTelemetryEvent.cs` | New `MonitorBlacklistCount` field |
| `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` | New localization keys |
| `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` | `BuiltInBlacklist`, `CustomBlacklist`, `DisplayedCustomBlacklist`, plus `AddCustomBlacklistEntry`/`UpdateCustomBlacklistEntry`/`DeleteCustomBlacklistEntry`/`SaveMonitorBlacklist`/`LoadMonitorBlacklist` |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml` | New `SettingsExpander` inside `PowerDisplay_AdvancedSettings` group |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs` | `AddBlacklistEntry_Click`, `EditBlacklistEntry_Click`, `DeleteBlacklistEntry_Click` |

No installer or WiX changes (data ships as embedded resource).

---

## Task 1: `MonitorBlacklistEntry` shared record

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistEntry.cs`

- [ ] **Step 1.1: Create the POCO**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// One entry in a PowerDisplay monitor blacklist. Used both for the built-in
    /// list shipped with PowerToys (loaded via <see cref="BuiltInMonitorBlacklist"/>)
    /// and for the user-editable custom list persisted on <c>PowerDisplayProperties</c>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="EdidId"/> is the 7–8 character PnP hardware identifier extracted
    /// from a <c>Monitor.Id</c> by <c>MonitorIdentity.EdidIdFromMonitorId</c> (e.g.
    /// <c>"DELD1A8"</c>, <c>"BOE0900"</c>). It is normalized to upper case and trimmed
    /// on write; matching is case-insensitive as a defense-in-depth measure.</para>
    /// <para><see cref="Comments"/> is free text rendered as-is. The built-in JSON ships
    /// English-only comments; user input is not localized.</para>
    /// </remarks>
    public class MonitorBlacklistEntry
    {
        [JsonPropertyName("edidId")]
        public string EdidId { get; set; } = string.Empty;

        [JsonPropertyName("comments")]
        public string Comments { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 1.2: Verify the project compiles**

Run from `src/modules/powerdisplay/PowerDisplay.Models/`:

```
dotnet build -c Debug
```

Expected: build succeeds; no other code references the new type yet.

- [ ] **Step 1.3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistEntry.cs
git commit -m "feat(PowerDisplay): introduce MonitorBlacklistEntry shared record"
```

---

## Task 2: Built-in JSON file + DTO + serialization context

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.json`
- Create: `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklistFile.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistSerializationContext.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplay.Models.csproj`

- [ ] **Step 2.1: Create the built-in JSON file (initially empty)**

Write `BuiltInMonitorBlacklist.json`:

```json
{
  "version": 1,
  "entries": []
}
```

The list ships empty. Future PRs add EdidId entries as reports come in.

- [ ] **Step 2.2: Create the DTO for the JSON file**

Write `BuiltInMonitorBlacklistFile.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// JSON file shape for <see cref="BuiltInMonitorBlacklist"/>.
    /// The <see cref="Version"/> field is a forward-compatibility marker; this
    /// release only understands version 1.
    /// </summary>
    public class BuiltInMonitorBlacklistFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("entries")]
        public List<MonitorBlacklistEntry> Entries { get; set; } = new();
    }
}
```

- [ ] **Step 2.3: Create the source-generated serialization context**

Write `MonitorBlacklistSerializationContext.cs` (mirrors `ProfileSerializationContext`):

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// JSON serialization context for monitor blacklist types.
    /// Provides source-generated serialization for Native AOT compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]
    [JsonSerializable(typeof(MonitorBlacklistEntry))]
    [JsonSerializable(typeof(List<MonitorBlacklistEntry>))]
    [JsonSerializable(typeof(BuiltInMonitorBlacklistFile))]
    public partial class MonitorBlacklistSerializationContext : JsonSerializerContext
    {
    }
}
```

- [ ] **Step 2.4: Register the JSON as an embedded resource**

Edit `PowerDisplay.Models.csproj`. Add a new `<ItemGroup>` (after the existing `<PropertyGroup>` blocks, before `</Project>`):

```xml
  <ItemGroup>
    <EmbeddedResource Include="BuiltInMonitorBlacklist.json" />
  </ItemGroup>
```

- [ ] **Step 2.5: Verify the project compiles**

```
dotnet build -c Debug src/modules/powerdisplay/PowerDisplay.Models
```

Expected: success.

- [ ] **Step 2.6: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.json \
        src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklistFile.cs \
        src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistSerializationContext.cs \
        src/modules/powerdisplay/PowerDisplay.Models/PowerDisplay.Models.csproj
git commit -m "feat(PowerDisplay): scaffold built-in monitor blacklist data file"
```

---

## Task 3: `BuiltInMonitorBlacklist` loader (TDD)

**Files:**
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/BuiltInMonitorBlacklistTests.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.cs`

- [ ] **Step 3.1: Write the failing test**

Write `BuiltInMonitorBlacklistTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class BuiltInMonitorBlacklistTests
{
    [TestMethod]
    public void Entries_LoadsWithoutThrowing()
    {
        var entries = BuiltInMonitorBlacklist.Entries;

        Assert.IsNotNull(entries);
    }

    [TestMethod]
    public void Entries_AreNormalizedToUpperCase()
    {
        foreach (var entry in BuiltInMonitorBlacklist.Entries)
        {
            Assert.AreEqual(entry.EdidId, entry.EdidId.ToUpperInvariant(),
                $"Entry '{entry.EdidId}' is not normalized to upper case.");
            Assert.AreEqual(entry.EdidId.Trim(), entry.EdidId,
                $"Entry '{entry.EdidId}' has untrimmed whitespace.");
        }
    }

    [TestMethod]
    public void Entries_ContainNoEmptyEdidIds()
    {
        Assert.IsFalse(
            BuiltInMonitorBlacklist.Entries.Any(e => string.IsNullOrWhiteSpace(e.EdidId)),
            "Built-in list should never contain blank EdidId entries.");
    }

    [TestMethod]
    public void Entries_AreCached()
    {
        var first = BuiltInMonitorBlacklist.Entries;
        var second = BuiltInMonitorBlacklist.Entries;

        Assert.AreSame(first, second, "Entries should be returned from a cached Lazy<>.");
    }
}
```

- [ ] **Step 3.2: Run the test to verify it fails**

```
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests --filter "FullyQualifiedName~BuiltInMonitorBlacklistTests"
```

Expected: compile error — `BuiltInMonitorBlacklist` does not exist.

- [ ] **Step 3.3: Implement the loader**

Write `BuiltInMonitorBlacklist.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Loads the built-in monitor blacklist shipped with PowerToys.
    /// The data is an embedded JSON resource in this assembly; the file is read once
    /// on first access and cached for the lifetime of the process.
    /// </summary>
    /// <remarks>
    /// Loader failures are non-fatal: on any exception (missing resource, malformed
    /// JSON, etc.) the loader returns an empty list. This keeps PowerDisplay running
    /// even if a malformed release ships, and avoids logging dependencies inside the
    /// AOT-compatible PowerDisplay.Models assembly.
    /// </remarks>
    public static class BuiltInMonitorBlacklist
    {
        private const string ResourceName = "PowerDisplay.Models.BuiltInMonitorBlacklist.json";

        private static readonly Lazy<IReadOnlyList<MonitorBlacklistEntry>> _entries
            = new(LoadFromResource);

        public static IReadOnlyList<MonitorBlacklistEntry> Entries => _entries.Value;

        private static IReadOnlyList<MonitorBlacklistEntry> LoadFromResource()
        {
            try
            {
                var assembly = typeof(BuiltInMonitorBlacklist).Assembly;
                using var stream = assembly.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    return Array.Empty<MonitorBlacklistEntry>();
                }

                var file = JsonSerializer.Deserialize(
                    stream,
                    MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile);

                if (file?.Entries == null)
                {
                    return Array.Empty<MonitorBlacklistEntry>();
                }

                return file.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.EdidId))
                    .Select(e => new MonitorBlacklistEntry
                    {
                        EdidId = e.EdidId.Trim().ToUpperInvariant(),
                        Comments = e.Comments ?? string.Empty,
                    })
                    .ToList();
            }
            catch
            {
                return Array.Empty<MonitorBlacklistEntry>();
            }
        }
    }
}
```

- [ ] **Step 3.4: Run the tests to verify they pass**

```
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests --filter "FullyQualifiedName~BuiltInMonitorBlacklistTests"
```

Expected: 4 tests pass.

- [ ] **Step 3.5: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/BuiltInMonitorBlacklistTests.cs
git commit -m "feat(PowerDisplay): load embedded built-in monitor blacklist"
```

---

## Task 4: `MonitorBlacklistService` (TDD)

**Files:**
- Test: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorBlacklistServiceTests.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorBlacklistService.cs`

- [ ] **Step 4.1: Write the failing tests**

Write `MonitorBlacklistServiceTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorBlacklistServiceTests
{
    private const string SamplePathDel  = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string SamplePathBoe  = @"\\?\DISPLAY#BOE0900#4&xyz&0&UID0";
    private const string SampleIdStripped = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

    [TestMethod]
    public void IsBlocked_EmptyService_ReturnsFalse()
    {
        var service = new MonitorBlacklistService(Array.Empty<MonitorBlacklistEntry>());

        Assert.IsFalse(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_MatchesCustomEntryByEdidId()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_MatchesStrippedMonitorId()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        // Both the raw DevicePath and the GUID-stripped Monitor.Id should match.
        Assert.IsTrue(service.IsBlocked(SampleIdStripped));
    }

    [TestMethod]
    public void IsBlocked_IsCaseInsensitive()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "deld1a8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
    }

    [TestMethod]
    public void IsBlocked_NormalizesWhitespace()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "   DELD1A8\t" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
    }

    [TestMethod]
    public void IsBlocked_EmptyOrUnknownMonitorId_ReturnsFalse()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsFalse(service.IsBlocked(string.Empty));
        Assert.IsFalse(service.IsBlocked(null!));
        Assert.IsFalse(service.IsBlocked(@"\\?\DISPLAY"));               // too few segments
        Assert.IsFalse(service.IsBlocked(@"garbage no hashes here"));
    }

    [TestMethod]
    public void IsBlocked_IgnoresBlankEntriesInList()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = string.Empty },
            new MonitorBlacklistEntry { EdidId = "   " },
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_BuiltInEntriesAreIncluded()
    {
        // Built-in list ships empty at the moment, but the service must still
        // consult it: a list that combines built-in (currently empty) with
        // custom entries should equal "just custom" today.
        var customOnly = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "BOE0900" },
        });

        Assert.IsTrue(customOnly.IsBlocked(SamplePathBoe));
        Assert.IsFalse(customOnly.IsBlocked(SamplePathDel));
    }
}
```

- [ ] **Step 4.2: Run the tests to verify they fail**

```
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests --filter "FullyQualifiedName~MonitorBlacklistServiceTests"
```

Expected: compile error — `MonitorBlacklistService` does not exist.

- [ ] **Step 4.3: Implement the service**

Write `MonitorBlacklistService.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Decides whether a monitor identified by its <c>Monitor.Id</c> (or raw DevicePath)
    /// should be filtered out of PowerDisplay's discovery. Matches on EdidId only —
    /// model-level granularity, so a single entry covers every physical port and every
    /// machine with the same monitor model.
    /// </summary>
    /// <remarks>
    /// The service is immutable; construct a new one each time the custom list changes.
    /// EdidIds are normalized (trimmed, upper-cased) on construction; comparisons use
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> as defense in depth.
    /// </remarks>
    public sealed class MonitorBlacklistService
    {
        private readonly HashSet<string> _blockedEdidIds;

        public MonitorBlacklistService(IEnumerable<MonitorBlacklistEntry> customEntries)
        {
            _blockedEdidIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in BuiltInMonitorBlacklist.Entries)
            {
                AddNormalized(entry.EdidId);
            }

            if (customEntries != null)
            {
                foreach (var entry in customEntries)
                {
                    AddNormalized(entry?.EdidId);
                }
            }
        }

        /// <summary>
        /// Returns true if <paramref name="monitorId"/> (a <c>Monitor.Id</c> or raw Windows
        /// DevicePath) has an EdidId in the union of built-in and custom blacklists.
        /// Monitors whose EdidId cannot be extracted (empty path, malformed) are never
        /// blocked — we only filter what we can positively identify.
        /// </summary>
        public bool IsBlocked(string monitorId)
        {
            var edid = MonitorIdentity.EdidIdFromMonitorId(monitorId);
            return !string.IsNullOrEmpty(edid) && _blockedEdidIds.Contains(edid);
        }

        private void AddNormalized(string? edidId)
        {
            var trimmed = edidId?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                _blockedEdidIds.Add(trimmed.ToUpperInvariant());
            }
        }
    }
}
```

- [ ] **Step 4.4: Run the tests to verify they pass**

```
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests --filter "FullyQualifiedName~MonitorBlacklistServiceTests"
```

Expected: 8 tests pass.

- [ ] **Step 4.5: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorBlacklistService.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorBlacklistServiceTests.cs
git commit -m "feat(PowerDisplay): add MonitorBlacklistService discovery gateway"
```

---

## Task 5: Persist custom blacklist in `PowerDisplayProperties`

**Files:**
- Modify: `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs`

- [ ] **Step 5.1: Add the new field**

Open `PowerDisplayProperties.cs`. In the constructor, after the existing line
`CustomVcpMappings = new List<CustomVcpValueMapping>();`, add:

```csharp
            MonitorBlacklist = new List<MonitorBlacklistEntry>();
```

Then add a new property after the existing `CustomVcpMappings` property (the file's last property today):

```csharp
        /// <summary>
        /// Gets or sets the user-editable list of monitor EDID IDs to skip entirely
        /// during PowerDisplay discovery. The effective blacklist is the union of this
        /// list and <c>BuiltInMonitorBlacklist.Entries</c>; matches are filtered out
        /// before any DDC/CI or WMI probing. See the design doc for the relationship
        /// to <see cref="MonitorInfo.IsHidden"/>.
        /// </summary>
        [JsonPropertyName("monitor_blacklist")]
        public List<MonitorBlacklistEntry> MonitorBlacklist { get; set; }
```

- [ ] **Step 5.2: Verify the project compiles**

```
dotnet build src/settings-ui/Settings.UI.Library
```

Expected: success.

- [ ] **Step 5.3: Manual round-trip sanity check (no automated test)**

Old `settings.json` files without `monitor_blacklist` should still deserialize. Confirm by reading `PowerDisplayProperties.cs` and noting:
- the constructor sets `MonitorBlacklist = new List<MonitorBlacklistEntry>()`, so deserialization onto a `new PowerDisplayProperties()` of a JSON without the key leaves the empty list intact;
- old PowerToys binaries reading a newer `settings.json` with `monitor_blacklist` simply ignore the unknown property (System.Text.Json default).

No code changes from this step — it's a check that the field is additive and the schema version does not need to bump (`PowerDisplaySettings.Version` stays `"1"`).

- [ ] **Step 5.4: Commit**

```bash
git add src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs
git commit -m "feat(PowerDisplay): persist custom monitor blacklist in settings"
```

---

## Task 6: Filter inventory in `MonitorManager`

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/MonitorManager.cs`

- [ ] **Step 6.1: Add the field and setter**

Open `MonitorManager.cs`. After the existing field declaration
`private readonly DisplayRotationService _rotationService = new();` (around line 32), add:

```csharp
        // Default to an empty custom list so discovery still works before MainViewModel
        // has had a chance to push the actual blacklist. Built-in entries are included
        // automatically by the service constructor.
        private MonitorBlacklistService _blacklistService
            = new(System.Array.Empty<MonitorBlacklistEntry>());
```

After `SetMaxCompatibilityMode(...)` (around line 79–85), add a sibling method:

```csharp
        /// <summary>
        /// Replaces the active <see cref="MonitorBlacklistService"/> with one built from the
        /// supplied custom entries (the built-in list is added automatically by the service
        /// constructor). Called by <see cref="ViewModels.MainViewModel"/> before each
        /// <see cref="DiscoverMonitorsAsync"/> so user edits to the blacklist take effect on
        /// the next refresh.
        /// </summary>
        public void SetMonitorBlacklist(System.Collections.Generic.IEnumerable<MonitorBlacklistEntry> customEntries)
        {
            _blacklistService = new MonitorBlacklistService(customEntries
                ?? System.Array.Empty<MonitorBlacklistEntry>());
        }
```

Add the necessary `using PowerDisplay.Models;` at the top of the file if it is not already present (it is required because `MonitorBlacklistEntry` lives there).

- [ ] **Step 6.2: Filter `inventory` before dispatch**

In `DiscoverFromAllControllersAsync`, immediately after the line
`var inventory = DisplayConfigInventory.GetAllMonitorDisplayInfo();` (around line 129)
and before the empty-check, insert:

```csharp
            // Filter blacklisted monitors out of the inventory before any controller
            // is dispatched. Matching uses MonitorIdentity.EdidIdFromMonitorId on each
            // entry's DevicePath, so blocked monitors are not opened, probed, or queried
            // — the whole point of the blacklist over the per-monitor IsHidden flag.
            var beforeCount = inventory.Count;
            var filteredInventory = new System.Collections.Generic.Dictionary<string, MonitorDisplayInfo>(
                inventory.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in inventory)
            {
                if (_blacklistService.IsBlocked(kvp.Value.DevicePath))
                {
                    Logger.LogInfo(
                        $"[MonitorBlacklist] Skipping '{kvp.Value.FriendlyName}' (path '{kvp.Value.DevicePath}') — EdidId is on the blacklist");
                    continue;
                }

                filteredInventory.Add(kvp.Key, kvp.Value);
            }

            if (filteredInventory.Count < beforeCount)
            {
                Logger.LogInfo(
                    $"[MonitorBlacklist] Filtered out {beforeCount - filteredInventory.Count} monitor(s); {filteredInventory.Count} remain");
            }

            inventory = filteredInventory;
```

Replace the variable name on the following lines so the rest of the method uses
the filtered dictionary instead of the raw one — but as a quick equivalent, the
final line above reassigns `inventory`. (`inventory` is a local; no further changes
needed.)

- [ ] **Step 6.3: Verify the project compiles**

```
dotnet build src/modules/powerdisplay/PowerDisplay
```

Expected: success.

- [ ] **Step 6.4: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/Helpers/MonitorManager.cs
git commit -m "feat(PowerDisplay): filter blacklisted monitors during discovery"
```

---

## Task 7: Push blacklist from `MainViewModel`

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs`

- [ ] **Step 7.1: Mirror `SetMaxCompatibilityMode` for the blacklist**

Open `MainViewModel.Monitors.cs`. Locate the two call sites of
`_monitorManager.SetMaxCompatibilityMode(...)` (one in `InitializeAsync`, around
line 32; one in `RefreshMonitorsAsync`, around line 114).

Immediately after each call to `SetMaxCompatibilityMode`, add:

```csharp
            _monitorManager.SetMonitorBlacklist(settings.Properties.MonitorBlacklist
                ?? new System.Collections.Generic.List<PowerDisplay.Models.MonitorBlacklistEntry>());
```

Use the same `settings` variable that is already in scope at each call site (the
file reads it via `_settingsUtils.GetSettingsOrDefault<...>` just before calling
`SetMaxCompatibilityMode`).

- [ ] **Step 7.2: Verify the project compiles**

```
dotnet build src/modules/powerdisplay/PowerDisplay
```

Expected: success.

- [ ] **Step 7.3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs
git commit -m "feat(PowerDisplay): propagate blacklist into MonitorManager before each discovery"
```

---

## Task 8: Telemetry — `MonitorBlacklistCount`

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Telemetry/Events/PowerDisplaySettingsTelemetryEvent.cs`

- [ ] **Step 8.1: Add the field**

Open `PowerDisplaySettingsTelemetryEvent.cs`. After the `ProfileCount` property,
add:

```csharp
        /// <summary>
        /// Number of user-customized monitor blacklist entries. Built-in entries
        /// are not reported (count is implicit per release). Specific EdidIds are
        /// intentionally not reported (privacy).
        /// </summary>
        public int MonitorBlacklistCount { get; set; }
```

- [ ] **Step 8.2: Populate the field where the event is emitted**

Search for the existing `new PowerDisplaySettingsTelemetryEvent` allocation:

```
grep -rn "new PowerDisplaySettingsTelemetryEvent" src/modules/powerdisplay
```

At each call site, add:

```csharp
                MonitorBlacklistCount = settings.Properties.MonitorBlacklist?.Count ?? 0,
```

next to the existing `ProfileCount = ...` assignment. If `settings` is named
differently in that file, adapt accordingly (the pattern is to read the same
settings reference that's already used to populate `ProfileCount`).

- [ ] **Step 8.3: Verify the project compiles**

```
dotnet build src/modules/powerdisplay/PowerDisplay
```

Expected: success.

- [ ] **Step 8.4: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/Telemetry/Events/PowerDisplaySettingsTelemetryEvent.cs \
        $(git diff --name-only)   # picks up any companion file the emitter lives in
git commit -m "feat(PowerDisplay): report custom blacklist entry count in telemetry"
```

(If `git diff --name-only` is empty because no other file changed, drop that argument.)

---

## Task 9: Localization strings

**Files:**
- Modify: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`

- [ ] **Step 9.1: Append new keys**

Open `Resources.resw`. Find the closing `</root>` tag at the bottom. Immediately
above it, add the following `<data>` blocks. The numbered comment in
square brackets is just guidance for the reader of this plan — don't include it
in the file.

```xml
  <data name="PowerDisplay_MonitorBlacklist.Header" xml:space="preserve">
    <value>Monitor blacklist</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist.Description" xml:space="preserve">
    <value>Models listed here are skipped entirely — PowerDisplay won't enumerate or probe them. Use this to work around monitors whose firmware misbehaves during DDC/CI probing.</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_BuiltInSubheader.Text" xml:space="preserve">
    <value>Built-in (managed by PowerToys)</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_BuiltInEmpty.Text" xml:space="preserve">
    <value>No built-in entries in this release.</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_CustomSubheader.Text" xml:space="preserve">
    <value>Your entries</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_AddButton.Content" xml:space="preserve">
    <value>Add EDID ID</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_EditButton.[ToolTipService.ToolTip]" xml:space="preserve">
    <value>Edit entry</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklist_DeleteButton.[ToolTipService.ToolTip]" xml:space="preserve">
    <value>Delete entry</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_Title" xml:space="preserve">
    <value>Blacklist entry</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_EdidLabel.Text" xml:space="preserve">
    <value>EDID ID</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_EdidPlaceholder.PlaceholderText" xml:space="preserve">
    <value>e.g. DELD1A8</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_CommentsLabel.Text" xml:space="preserve">
    <value>Comments (optional)</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_Validation_InvalidEdid" xml:space="preserve">
    <value>EDID ID must be 1–16 alphanumeric characters.</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfBuiltIn" xml:space="preserve">
    <value>This EDID is already in the built-in list. You can still add it; it will be ignored in the displayed custom list.</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfCustom" xml:space="preserve">
    <value>This EDID is already in your custom list.</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_PrimaryButton" xml:space="preserve">
    <value>Save</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistEditor_CloseButton" xml:space="preserve">
    <value>Cancel</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistDelete_Title" xml:space="preserve">
    <value>Delete blacklist entry?</value>
  </data>
  <data name="PowerDisplay_MonitorBlacklistDelete_Content" xml:space="preserve">
    <value>This will allow PowerDisplay to discover monitors with this EDID again.</value>
  </data>
```

- [ ] **Step 9.2: Verify well-formed XML**

```
dotnet build src/settings-ui/Settings.UI
```

Expected: build succeeds. (resw is parsed at build time.)

- [ ] **Step 9.3: Commit**

```bash
git add src/settings-ui/Settings.UI/Strings/en-us/Resources.resw
git commit -m "feat(PowerDisplay): add resw strings for monitor blacklist UI"
```

---

## Task 10: Editor dialog — XAML + code-behind

**Files:**
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml`
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml.cs`

- [ ] **Step 10.1: Write the XAML**

Create `MonitorBlacklistEditorDialog.xaml`:

```xml
<ContentDialog
    x:Class="Microsoft.PowerToys.Settings.UI.Views.MonitorBlacklistEditorDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d"
    DefaultButton="Primary">
    <StackPanel Spacing="12" Width="380">
        <TextBlock x:Uid="PowerDisplay_MonitorBlacklistEditor_EdidLabel" />
        <TextBox
            x:Name="EdidIdTextBox"
            x:Uid="PowerDisplay_MonitorBlacklistEditor_EdidPlaceholder"
            CharacterCasing="Upper"
            MaxLength="16"
            TextChanged="EdidIdTextBox_TextChanged" />

        <TextBlock x:Uid="PowerDisplay_MonitorBlacklistEditor_CommentsLabel" />
        <TextBox
            x:Name="CommentsTextBox"
            AcceptsReturn="True"
            MaxLength="200"
            TextWrapping="Wrap" />

        <InfoBar
            x:Name="ValidationInfoBar"
            IsClosable="False"
            IsOpen="False"
            Severity="Warning" />
    </StackPanel>
</ContentDialog>
```

- [ ] **Step 10.2: Write the code-behind**

Create `MonitorBlacklistEditorDialog.xaml.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog for creating or editing one user-customized monitor blacklist entry.
    /// EdidId input is forced upper-case at typing time and validated against
    /// <c>[A-Za-z0-9]{1,16}</c>; duplicates against the built-in or existing custom
    /// list show informational warnings (the user is still allowed to add them per
    /// design — the displayed list de-duplicates).
    /// </summary>
    public sealed partial class MonitorBlacklistEditorDialog : ContentDialog
    {
        private static readonly Regex EdidIdRegex = new("^[A-Za-z0-9]{1,16}$", RegexOptions.Compiled);

        private readonly HashSet<string> _builtInIds;
        private readonly HashSet<string> _existingCustomIds;
        private readonly string? _originalEdidId;

        /// <summary>
        /// Gets the entry produced by the dialog after Save. Null if the dialog was cancelled.
        /// </summary>
        public MonitorBlacklistEntry? ResultEntry { get; private set; }

        public MonitorBlacklistEditorDialog(
            IEnumerable<MonitorBlacklistEntry> builtIn,
            IEnumerable<MonitorBlacklistEntry> existingCustom,
            MonitorBlacklistEntry? existing = null)
        {
            this.InitializeComponent();

            _builtInIds = new HashSet<string>(
                builtIn.Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);

            _existingCustomIds = new HashSet<string>(
                existingCustom.Select(e => e.EdidId.ToUpperInvariant()),
                System.StringComparer.OrdinalIgnoreCase);

            _originalEdidId = existing?.EdidId;

            // Editing an existing entry: pre-fill, and remove its own EdidId from the
            // duplicate-of-custom set so saving without changing the EdidId is allowed.
            if (existing != null)
            {
                EdidIdTextBox.Text = existing.EdidId;
                CommentsTextBox.Text = existing.Comments ?? string.Empty;
                _existingCustomIds.Remove(existing.EdidId.ToUpperInvariant());
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_PrimaryButton");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_MonitorBlacklistEditor_CloseButton");

            this.PrimaryButtonClick += OnPrimaryButtonClick;
            UpdateValidationState();
        }

        private void EdidIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateValidationState();

        private void UpdateValidationState()
        {
            var input = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            if (!EdidIdRegex.IsMatch(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_InvalidEdid");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = false;
                return;
            }

            if (_builtInIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfBuiltIn");
                ValidationInfoBar.Severity = InfoBarSeverity.Informational;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = true; // allowed; UI dedups
                return;
            }

            if (_existingCustomIds.Contains(input))
            {
                ValidationInfoBar.Message = resourceLoader.GetString(
                    "PowerDisplay_MonitorBlacklistEditor_Validation_DuplicateOfCustom");
                ValidationInfoBar.Severity = InfoBarSeverity.Warning;
                ValidationInfoBar.IsOpen = true;
                IsPrimaryButtonEnabled = false;
                return;
            }

            ValidationInfoBar.IsOpen = false;
            IsPrimaryButtonEnabled = true;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var edid = (EdidIdTextBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (!EdidIdRegex.IsMatch(edid))
            {
                args.Cancel = true;
                return;
            }

            ResultEntry = new MonitorBlacklistEntry
            {
                EdidId = edid,
                Comments = (CommentsTextBox.Text ?? string.Empty).Trim(),
            };
        }
    }
}
```

- [ ] **Step 10.3: Verify the project compiles**

```
dotnet build src/settings-ui/Settings.UI
```

Expected: success.

- [ ] **Step 10.4: Commit**

```bash
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml \
        src/settings-ui/Settings.UI/SettingsXAML/Views/MonitorBlacklistEditorDialog.xaml.cs
git commit -m "feat(PowerDisplay): add monitor-blacklist editor dialog"
```

---

## Task 11: ViewModel — blacklist collections + commands

**Files:**
- Modify: `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs`

- [ ] **Step 11.1: Add fields**

Find the existing `_customVcpMappings` field declaration. Below it, add:

```csharp
        private ObservableCollection<MonitorBlacklistEntry> _builtInBlacklist = new();
        private ObservableCollection<MonitorBlacklistEntry> _customBlacklist = new();
        private ObservableCollection<MonitorBlacklistEntry> _displayedCustomBlacklist = new();
```

If the file does not already have it, add `using PowerDisplay.Models;` to the using
block at the top.

- [ ] **Step 11.2: Add public properties**

Below the existing `CustomVcpMappings`/`HasCustomVcpMappings` properties (around
lines 656–661), add:

```csharp
        public ObservableCollection<MonitorBlacklistEntry> BuiltInBlacklist => _builtInBlacklist;

        public bool HasBuiltInBlacklist => _builtInBlacklist?.Count > 0;

        public ObservableCollection<MonitorBlacklistEntry> CustomBlacklist => _customBlacklist;

        /// <summary>
        /// Gets the custom blacklist with entries already present in the built-in list filtered
        /// out. The underlying <see cref="CustomBlacklist"/> (and persisted settings) still hold
        /// the user's full list — duplicates are honored but hidden from the UI to keep
        /// "Your entries" visually distinct from "Built-in".
        /// </summary>
        public ObservableCollection<MonitorBlacklistEntry> DisplayedCustomBlacklist => _displayedCustomBlacklist;

        public bool HasMonitorBlacklist
            => HasBuiltInBlacklist || _displayedCustomBlacklist?.Count > 0;
```

- [ ] **Step 11.3: Initialize / load on construction**

Find `LoadCustomVcpMappings()` (around line 851). Below it, add a sibling method:

```csharp
        private void LoadMonitorBlacklist()
        {
            _builtInBlacklist = new ObservableCollection<MonitorBlacklistEntry>(BuiltInMonitorBlacklist.Entries);

            var custom = _settings.Properties.MonitorBlacklist ?? new List<MonitorBlacklistEntry>();
            _customBlacklist = new ObservableCollection<MonitorBlacklistEntry>(custom);

            RebuildDisplayedCustomBlacklist();

            _customBlacklist.CollectionChanged += (s, e) =>
            {
                RebuildDisplayedCustomBlacklist();
                OnPropertyChanged(nameof(HasMonitorBlacklist));
            };

            OnPropertyChanged(nameof(BuiltInBlacklist));
            OnPropertyChanged(nameof(HasBuiltInBlacklist));
            OnPropertyChanged(nameof(CustomBlacklist));
            OnPropertyChanged(nameof(DisplayedCustomBlacklist));
            OnPropertyChanged(nameof(HasMonitorBlacklist));
        }

        private void RebuildDisplayedCustomBlacklist()
        {
            var builtInIds = new HashSet<string>(
                _builtInBlacklist.Select(e => e.EdidId.ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            // Reuse the existing ObservableCollection instance so XAML bindings don't tear down.
            _displayedCustomBlacklist.Clear();
            foreach (var entry in _customBlacklist)
            {
                if (!builtInIds.Contains(entry.EdidId.ToUpperInvariant()))
                {
                    _displayedCustomBlacklist.Add(entry);
                }
            }

            OnPropertyChanged(nameof(DisplayedCustomBlacklist));
            OnPropertyChanged(nameof(HasMonitorBlacklist));
        }
```

Then add `LoadMonitorBlacklist();` to the constructor immediately after the
existing `LoadCustomVcpMappings();` call (around line 79).

- [ ] **Step 11.4: Add Add/Update/Delete methods**

After the existing `DeleteCustomVcpMapping(...)` method (around line 909–923), add:

```csharp
        public void AddCustomBlacklistEntry(MonitorBlacklistEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EdidId))
            {
                return;
            }

            entry.EdidId = entry.EdidId.Trim().ToUpperInvariant();
            entry.Comments = (entry.Comments ?? string.Empty).Trim();

            _customBlacklist.Add(entry);
            SaveMonitorBlacklist();
        }

        public void UpdateCustomBlacklistEntry(MonitorBlacklistEntry oldEntry, MonitorBlacklistEntry newEntry)
        {
            if (oldEntry == null || newEntry == null)
            {
                return;
            }

            var index = _customBlacklist.IndexOf(oldEntry);
            if (index < 0)
            {
                return;
            }

            newEntry.EdidId = newEntry.EdidId.Trim().ToUpperInvariant();
            newEntry.Comments = (newEntry.Comments ?? string.Empty).Trim();

            _customBlacklist[index] = newEntry;
            SaveMonitorBlacklist();
        }

        public void DeleteCustomBlacklistEntry(MonitorBlacklistEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_customBlacklist.Remove(entry))
            {
                SaveMonitorBlacklist();
            }
        }

        private void SaveMonitorBlacklist()
        {
            _settings.Properties.MonitorBlacklist = _customBlacklist.ToList();
            NotifySettingsChanged();
        }
```

If a private helper `NotifySettingsChanged()` does not exist in the file, replace
that call with whatever pattern the file already uses for `CustomVcpMappings`
saves (see `SaveCustomVcpMappings` around line 926 — copy the body that writes
settings.json and pings IPC).

- [ ] **Step 11.5: Verify the project compiles**

```
dotnet build src/settings-ui/Settings.UI
```

Expected: success.

- [ ] **Step 11.6: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs
git commit -m "feat(PowerDisplay): expose monitor blacklist on the settings ViewModel"
```

---

## Task 12: Surface the expander on the page

**Files:**
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml`

- [ ] **Step 12.1: Insert the expander in the Advanced settings group**

Open `PowerDisplayPage.xaml`. Find the existing `PowerDisplay_AdvancedSettings`
SettingsGroup (around lines 104–108) — it currently contains only the
`PowerDisplay_MaxCompatibilityMode` SettingsCard. Replace that group's content
with:

```xml
                        <controls:SettingsGroup x:Uid="PowerDisplay_AdvancedSettings" IsEnabled="{x:Bind ViewModel.IsEnabled, Mode=OneWay}">
                            <tkcontrols:SettingsCard x:Uid="PowerDisplay_MaxCompatibilityMode" HeaderIcon="{ui:FontIcon Glyph=&#xE7B5;}">
                                <ToggleSwitch IsOn="{x:Bind ViewModel.MaxCompatibilityMode, Mode=TwoWay}" />
                            </tkcontrols:SettingsCard>

                            <!--  Monitor blacklist  -->
                            <tkcontrols:SettingsExpander
                                x:Uid="PowerDisplay_MonitorBlacklist"
                                HeaderIcon="{ui:FontIcon Glyph=&#xE72E;}"
                                IsExpanded="{x:Bind ViewModel.HasMonitorBlacklist, Mode=OneWay}">
                                <tkcontrols:SettingsExpander.Items>
                                    <!--  Built-in subheader  -->
                                    <tkcontrols:SettingsCard ContentAlignment="Left">
                                        <TextBlock
                                            x:Uid="PowerDisplay_MonitorBlacklist_BuiltInSubheader"
                                            FontWeight="SemiBold" />
                                    </tkcontrols:SettingsCard>

                                    <!--  Built-in list (read-only)  -->
                                    <tkcontrols:SettingsCard ContentAlignment="Left" Visibility="{x:Bind ViewModel.HasBuiltInBlacklist, Mode=OneWay, Converter={StaticResource ReverseBoolToVisibilityConverter}}">
                                        <TextBlock
                                            x:Uid="PowerDisplay_MonitorBlacklist_BuiltInEmpty"
                                            Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                    </tkcontrols:SettingsCard>

                                    <tkcontrols:SettingsCard ContentAlignment="Left" Visibility="{x:Bind ViewModel.HasBuiltInBlacklist, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}">
                                        <ItemsControl ItemsSource="{x:Bind ViewModel.BuiltInBlacklist, Mode=OneWay}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate x:DataType="pdmodels:MonitorBlacklistEntry">
                                                    <Grid Margin="0,4,0,4" ColumnSpacing="12">
                                                        <Grid.ColumnDefinitions>
                                                            <ColumnDefinition Width="Auto" />
                                                            <ColumnDefinition Width="*" />
                                                        </Grid.ColumnDefinitions>
                                                        <TextBlock FontFamily="Consolas" Text="{x:Bind EdidId}" />
                                                        <TextBlock
                                                            Grid.Column="1"
                                                            Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                                            Text="{x:Bind Comments}"
                                                            TextWrapping="Wrap" />
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </tkcontrols:SettingsCard>

                                    <!--  Custom subheader  -->
                                    <tkcontrols:SettingsCard ContentAlignment="Left">
                                        <TextBlock
                                            x:Uid="PowerDisplay_MonitorBlacklist_CustomSubheader"
                                            FontWeight="SemiBold" />
                                    </tkcontrols:SettingsCard>

                                    <!--  Custom list (editable)  -->
                                    <tkcontrols:SettingsCard ContentAlignment="Left">
                                        <ItemsControl ItemsSource="{x:Bind ViewModel.DisplayedCustomBlacklist, Mode=OneWay}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate x:DataType="pdmodels:MonitorBlacklistEntry">
                                                    <Grid Margin="0,4,0,4" ColumnSpacing="12">
                                                        <Grid.ColumnDefinitions>
                                                            <ColumnDefinition Width="Auto" />
                                                            <ColumnDefinition Width="*" />
                                                            <ColumnDefinition Width="Auto" />
                                                        </Grid.ColumnDefinitions>
                                                        <TextBlock VerticalAlignment="Center" FontFamily="Consolas" Text="{x:Bind EdidId}" />
                                                        <TextBlock
                                                            Grid.Column="1"
                                                            VerticalAlignment="Center"
                                                            Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                                            Text="{x:Bind Comments}"
                                                            TextWrapping="Wrap" />
                                                        <StackPanel
                                                            Grid.Column="2"
                                                            Orientation="Horizontal"
                                                            Spacing="{StaticResource PowerDisplayActionButtonSpacing}">
                                                            <Button
                                                                x:Uid="PowerDisplay_MonitorBlacklist_EditButton"
                                                                Click="EditBlacklistEntry_Click"
                                                                Content="{ui:FontIcon Glyph=&#xE70F;}"
                                                                Style="{StaticResource SubtleButtonStyle}"
                                                                Tag="{x:Bind}" />
                                                            <Button
                                                                x:Uid="PowerDisplay_MonitorBlacklist_DeleteButton"
                                                                Click="DeleteBlacklistEntry_Click"
                                                                Content="{ui:FontIcon Glyph=&#xE74D;}"
                                                                Style="{StaticResource SubtleButtonStyle}"
                                                                Tag="{x:Bind}" />
                                                        </StackPanel>
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </tkcontrols:SettingsCard>

                                    <!--  Add button  -->
                                    <tkcontrols:SettingsCard ContentAlignment="Left">
                                        <Button x:Uid="PowerDisplay_MonitorBlacklist_AddButton" Click="AddBlacklistEntry_Click" />
                                    </tkcontrols:SettingsCard>
                                </tkcontrols:SettingsExpander.Items>
                            </tkcontrols:SettingsExpander>
                        </controls:SettingsGroup>
```

- [ ] **Step 12.2: Verify the XAML compiles**

```
dotnet build src/settings-ui/Settings.UI
```

Expected: success (warnings about new event handlers not yet existing are fine —
those are wired in Task 13 right after).

- [ ] **Step 12.3: Commit**

```bash
git add src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml
git commit -m "feat(PowerDisplay): surface monitor blacklist in advanced settings"
```

(If the build in 12.2 fails because the click handlers truly are required at
compile time, defer this commit until after Task 13 and squash 12+13 manually.)

---

## Task 13: Page click handlers

**Files:**
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs`

- [ ] **Step 13.1: Add the three click handlers**

Open `PowerDisplayPage.xaml.cs`. After the existing `DeleteCustomMapping_Click`
method (around line 217), add:

```csharp
        // Monitor blacklist event handlers
        private async void AddBlacklistEntry_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MonitorBlacklistEditorDialog(
                ViewModel.BuiltInBlacklist,
                ViewModel.CustomBlacklist)
            {
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.ResultEntry != null)
            {
                ViewModel.AddCustomBlacklistEntry(dialog.ResultEntry);
            }
        }

        private async void EditBlacklistEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not MonitorBlacklistEntry entry)
            {
                return;
            }

            var dialog = new MonitorBlacklistEditorDialog(
                ViewModel.BuiltInBlacklist,
                ViewModel.CustomBlacklist,
                entry)
            {
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.ResultEntry != null)
            {
                ViewModel.UpdateCustomBlacklistEntry(entry, dialog.ResultEntry);
            }
        }

        private async void DeleteBlacklistEntry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not MonitorBlacklistEntry entry)
            {
                return;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("PowerDisplay_MonitorBlacklistDelete_Title"),
                Content = resourceLoader.GetString("PowerDisplay_MonitorBlacklistDelete_Content"),
                PrimaryButtonText = resourceLoader.GetString("Yes"),
                CloseButtonText = resourceLoader.GetString("No"),
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeleteCustomBlacklistEntry(entry);
            }
        }
```

- [ ] **Step 13.2: Verify the project compiles**

```
dotnet build src/settings-ui/Settings.UI
```

Expected: success.

- [ ] **Step 13.3: Commit**

```bash
git add src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs
git commit -m "feat(PowerDisplay): wire monitor blacklist add/edit/delete handlers"
```

---

## Task 14: Build + test verification

**Files:** none modified — verification only.

- [ ] **Step 14.1: Run all PowerDisplay unit tests**

```
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests
```

Expected: all tests pass (existing + the 12 added in Tasks 3 and 4).

- [ ] **Step 14.2: Full Settings.UI + PowerDisplay build**

```
dotnet build src/settings-ui/Settings.UI src/modules/powerdisplay/PowerDisplay
```

Expected: success across both projects.

- [ ] **Step 14.3: Manual smoke test (out-of-CI)**

1. Run Settings.UI; open PowerDisplay page. Confirm the "Monitor blacklist"
   expander appears under "Advanced settings", lists the built-in section
   (empty), and offers an "Add EDID ID" button.
2. Click Add → enter a connected monitor's EdidId (read it from the existing
   Monitors list above; each monitor row shows the stable ID — the EdidId is
   the segment between the first two `#`). Save.
3. The monitor disappears from the Monitor list and from the PowerDisplay
   flyout within one refresh cycle (or after toggling the module off/on).
4. Delete the entry. The monitor reappears with all per-monitor settings
   (Hidden, Enable* toggles) intact.
5. Edit an existing entry: confirm the EdidId duplicate check accepts the
   unchanged value and rejects a different existing custom EdidId.

- [ ] **Step 14.4: Final commit (only if a fix surfaced during smoke test)**

If the smoke test surfaced no issues, no commit. If it did, fix and commit:

```bash
git add <files>
git commit -m "fix(PowerDisplay): <symptom>"
```

---

## Plan Self-Review

**Spec coverage:**

| Spec section | Task(s) |
| --- | --- |
| 4.1 Data Model | 1 |
| 4.2 Built-in Blacklist Source | 2, 3 |
| 4.3 Custom Blacklist Persistence | 5 |
| 4.4 Discovery Gateway | 4, 6, 7 |
| 4.5 Settings UI | 10, 11, 12, 13 |
| 4.6 Localization | 9 |
| 4.7 Telemetry | 8 |
| 4.8 Settings Reload Flow | 7 (reuses existing path; no new code beyond the SetMonitorBlacklist call) |
| 4.9 Backward Compatibility | 5 (constructor default + non-bumped Version) |
| 5 Testing | 3 (loader), 4 (service), 14 (manual UI smoke) |

**Placeholder scan:** no TBD, no "implement later", no abstract "add error handling". Every step has the actual code or command.

**Type consistency:**

- `MonitorBlacklistEntry.EdidId` (string) and `.Comments` (string) referenced consistently across Tasks 1, 2, 4, 5, 10, 11, 12, 13.
- `MonitorBlacklistService.IsBlocked(string)` referenced consistently in Tasks 4, 6.
- `BuiltInMonitorBlacklist.Entries` (`IReadOnlyList<MonitorBlacklistEntry>`) referenced in Tasks 3, 4, 11.
- ViewModel method names — `AddCustomBlacklistEntry`, `UpdateCustomBlacklistEntry`, `DeleteCustomBlacklistEntry` — match between Tasks 11 and 13 exactly.
- `_blacklistService` field name consistent in Task 6.

**Known plan flex points** (decided during plan execution by the implementer):
- Task 6's `inventory` reassignment vs. variable rename — pick whichever is cleaner.
- Task 8's emitter call site — discovered via `grep`, may need a minor adapter line.
- Task 11's `SaveMonitorBlacklist` save mechanism — should reuse exactly whatever pattern `SaveCustomVcpMappings` already uses in the file.

These flex points are appropriate at the implementation layer — the plan locks the contract; the local mechanism can match local convention.

