# PowerDisplay Monitor Blacklist — Design

Status: Approved 2026-05-21 (Yu Leng)
Owner: yuleng@microsoft.com
Module: `src/modules/powerdisplay/`, `src/settings-ui/`

## 1. Summary

Add a model-level monitor blacklist to PowerDisplay. Entries are matched by
**EdidId** (the 7–8-character PnP hardware ID such as `DELD1A8`, extracted from
`Monitor.Id` via `MonitorIdentity.EdidIdFromMonitorId`). When a discovered
monitor's EdidId matches the union of (built-in ∪ custom) blacklists, PowerDisplay
filters it out at the discovery stage — before any DDC/CI or WMI probing — so the
monitor is completely invisible to the rest of the app (flyout, Settings monitor
list, profiles, restore-on-startup).

Two sources:

- **Built-in** — an embedded JSON shipped with PowerToys, distributed via the
  PowerDisplay.Models assembly. Read-only; managed by Microsoft via PRs.
- **Custom** — a user-editable list under PowerDisplay settings. Manual EdidId
  input only.

The UI lives inside the existing **Advanced settings** group on the
PowerDisplay Settings page.

## 2. Background & Motivation

Some monitor models reliably cause problems when PowerDisplay enumerates or
probes them — for example, sending a DDC/CI capabilities request hangs the
firmware, or a WMI brightness instance returns garbage that destabilizes the
controller. The existing per-monitor `IsHidden` flag does not help in those
cases because:

1. `IsHidden` only suppresses the monitor in the flyout; PowerDisplay still
   discovers and probes it on every refresh.
2. `IsHidden` is keyed by full stable `Monitor.Id` (per physical port), so the
   user has to wait until the bad monitor has been discovered at least once
   before they can mark it — by which point the crash may already have
   occurred.
3. `IsHidden` is per-instance, so two physically identical bad monitors require
   two flags; a "this whole model is bad" notion does not exist.

A model-level (EdidId-based) blacklist applied before probing fills these gaps.
The built-in list lets us protect all users from known-bad models via a release;
the custom list gives users self-service for models that aren't yet covered.

### Relationship to existing `IsHidden`

`IsHidden` is **kept unchanged** and serves a complementary role:

| Aspect | `IsHidden` (existing) | Blacklist (new) |
| --- | --- | --- |
| Trigger time | After discovery; user must see the monitor first | At discovery; works even before the device is plugged in |
| DDC/CI probing | Still happens | Skipped entirely |
| Granularity | Single device (full stable `Monitor.Id`) | Model (`EdidId`) |
| Persistence | `MonitorInfo.IsHidden` in `Properties.Monitors[]` | `Properties.MonitorBlacklist[]` |
| Intended use | "I don't want this one in my flyout" | "Probing this model breaks things" |

If a monitor is both `IsHidden=true` and blacklisted, blacklist wins (it's
strictly stronger). `IsHidden` state is left alone for the user's record.

## 3. Goals / Non-goals

### Goals

- Filter out blacklisted monitors at discovery time, before any DDC/CI or WMI
  probing — so a blacklist entry can prevent crashes/hangs caused by probing
  itself.
- Ship a curated built-in list with PowerToys releases.
- Let users add their own EdidId entries via Settings, and remove them later.
- Make the matching case-insensitive and whitespace-tolerant.
- Keep settings.json schema backward-compatible (no version bump).

### Non-goals

- No remote/online updates of the built-in list. Updates flow via product
  releases.
- No support for blacklisting a single physical port (stable-`Monitor.Id`
  granularity). That is what `IsHidden` already does; offering it in the
  blacklist would duplicate the mechanism with no added value.
- No "exempt from built-in" mechanism for users. The built-in list is product
  policy.
- No friendly model-name field (e.g., "Dell U2723QE"). Names rot; EdidId +
  free-text `comments` is enough.
- No telemetry on the specific EdidIds users blacklist (privacy). We only
  report the count.

## 4. Detailed Design

### 4.1 Data Model

A single shared record type, used identically by built-in and custom entries.
Lives in `PowerDisplay.Models` so both the PowerDisplay app and the
Settings.UI.Library can reference it.

```csharp
// src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistEntry.cs
public class MonitorBlacklistEntry
{
    [JsonPropertyName("edidId")]
    public string EdidId { get; set; } = string.Empty;   // Normalized: Trim().ToUpperInvariant()

    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty; // Free text, displayed as-is in UI
}
```

Normalization rule (applied on read, write, and user input):
`Trim().ToUpperInvariant()`. Matching uses
`StringComparer.OrdinalIgnoreCase` as a defense-in-depth measure in case any
code path bypasses normalization.

Validation rule for user input: `^[A-Za-z0-9]{1,16}$` after trimming. Typical
EdidIds are 7–8 chars (3-letter vendor + 4-hex product code), but the regex is
relaxed to accommodate edge cases.

### 4.2 Built-in Blacklist Source

JSON shipped as an embedded resource in `PowerDisplay.Models`:

```
src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.json
```

Schema:

```json
{
  "version": 1,
  "entries": [
    { "edidId": "EXAMPLE1", "comments": "Hangs during DDC/CI probe (GH #xxxxx)" }
  ]
}
```

Loader:

```csharp
// src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.cs
public static class BuiltInMonitorBlacklist
{
    private static readonly Lazy<IReadOnlyList<MonitorBlacklistEntry>> _entries
        = new(LoadFromResource);

    public static IReadOnlyList<MonitorBlacklistEntry> Entries => _entries.Value;

    private static IReadOnlyList<MonitorBlacklistEntry> LoadFromResource()
    {
        // Read embedded resource stream, deserialize via System.Text.Json
        // source-generated context (see existing ProfileSerializationContext for
        // pattern). Normalize EdidId on the way out. On any parse error,
        // log and return empty list — built-in must never crash the app.
    }
}
```

Project file change in `PowerDisplay.Models.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="BuiltInMonitorBlacklist.json" />
</ItemGroup>
```

`version` is a forward-compatibility marker. If the schema needs to evolve,
the loader will use `version` to pick the right parser. For this PR only
`version: 1` exists.

The initial JSON ships with an **empty** `entries` array (no monitors are
on the built-in list at launch). Microsoft adds entries via subsequent PRs
as issues are reported.

### 4.3 Custom Blacklist Persistence

Add one field to `PowerDisplayProperties`:

```csharp
// src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs
[JsonPropertyName("monitor_blacklist")]
public List<MonitorBlacklistEntry> MonitorBlacklist { get; set; } = new();
```

- Initialized to empty list in the constructor (so old settings.json files
  without the key deserialize cleanly).
- `PowerDisplaySettings.Version` is **not** bumped; the schema change is
  additive.
- No migration code needed.

### 4.4 Discovery Gateway

A new service in `PowerDisplay.Lib`:

```csharp
// src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorBlacklistService.cs
public sealed class MonitorBlacklistService
{
    private readonly HashSet<string> _blockedEdidIds;

    public MonitorBlacklistService(IEnumerable<MonitorBlacklistEntry> customEntries)
    {
        _blockedEdidIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in BuiltInMonitorBlacklist.Entries)
        {
            AddNormalized(e.EdidId);
        }

        foreach (var e in customEntries)
        {
            AddNormalized(e.EdidId);
        }
    }

    public bool IsBlocked(string monitorId)
    {
        var edid = MonitorIdentity.EdidIdFromMonitorId(monitorId);
        return !string.IsNullOrEmpty(edid) && _blockedEdidIds.Contains(edid);
    }

    private void AddNormalized(string edidId)
    {
        var n = edidId?.Trim();
        if (!string.IsNullOrEmpty(n))
        {
            _blockedEdidIds.Add(n.ToUpperInvariant());
        }
    }
}
```

**Integration point.** The service is consulted in the monitor enumeration
path before any DDC/CI or WMI probing. Concretely:

- `MonitorDiscoveryHelper` (or its caller `MonitorManager.DiscoverMonitorsAsync`
  in `PowerDisplay.Lib`) collects raw display paths via QueryDisplayConfig.
- Immediately after that step — and before opening physical-monitor handles or
  reading VCP capabilities — each candidate's device path is fed through
  `MonitorIdentity.EdidIdFromMonitorId` and checked against the service.
- Hits are dropped from the returned list.

Because `EdidIdFromMonitorId` is pure string parsing of a path already in hand,
filtering does not require touching the device — which is precisely what makes
this useful for crash-prone monitors.

**Lifecycle.** A fresh `MonitorBlacklistService` instance is constructed each
time the PowerDisplay app reloads settings (i.e., on the existing settings-
changed signal path). `MonitorManager` holds the current instance and consults
it on each `DiscoverMonitorsAsync`.

### 4.5 Settings UI

Lives in the **Advanced settings** group on the PowerDisplay page, immediately
after the existing `PowerDisplay_MaxCompatibilityMode` card.

```
Advanced settings
├─ ☐ Max compatibility mode
└─ ▼ Monitor blacklist
   "Models listed here are skipped entirely — PowerDisplay won't enumerate or probe them."
   ───────────────────────────────────────────────────────────────────
   Built-in (managed by PowerToys)
     EXAMPLE1   "Hangs during DDC/CI probe (GH #xxxxx)"
     EXAMPLE2   "Reports invalid capabilities"
   ───────────────────────────────────────────────────────────────────
   Your entries
     CUSTOM1    "My buggy monitor"                          [✏] [🗑]
     ─────────────────────────────────────
     [ + Add EDID ID ]
```

Implementation mirrors the existing `PowerDisplay_CustomVcpMappings` pattern in
[PowerDisplayPage.xaml:111-148](../../src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml):

- Single outer `SettingsExpander` with header "Monitor blacklist" and a short
  description.
- Two `ItemsControl` blocks inside, separated by sub-headers and a thin
  divider:
  - **Built-in** bound to `ViewModel.BuiltInBlacklist` — read-only, each item
    shows EdidId + comments, no buttons.
  - **Your entries** bound to `ViewModel.DisplayedCustomBlacklist` — each item
    shows EdidId + comments + edit/delete buttons; below it an "+ Add EDID ID"
    button.
- Add button opens a `ContentDialog` with two inputs (EdidId, Comments).
  - Live normalization: input is `ToUpperInvariant()`-ed on text changed.
  - Validation: regex above; submit disabled when invalid.
  - Duplicate detection (validation surfaced inline on the dialog):
    - If matches a built-in EdidId → InfoBar "This EDID is already in the
      built-in list. You can still add it; it will be ignored."
    - If matches an existing custom EdidId → InfoBar "This EDID is already in
      your custom list."
- Edit button reuses the same dialog pre-filled.
- Delete button removes from the list immediately (no confirmation — destructive
  action is trivially reversible by re-adding).

**ViewModel additions** in `PowerDisplayViewModel`:

```csharp
public ObservableCollection<MonitorBlacklistEntry> BuiltInBlacklist { get; }
public ObservableCollection<MonitorBlacklistEntry> CustomBlacklist { get; }
public ObservableCollection<MonitorBlacklistEntry> DisplayedCustomBlacklist { get; }
public bool HasMonitorBlacklist { get; }   // expansion default
```

- `BuiltInBlacklist` is populated once from `BuiltInMonitorBlacklist.Entries`.
- `CustomBlacklist` is two-way bound to `Properties.MonitorBlacklist`; changes
  trigger `SettingsUtils.SaveSettings`.
- `DisplayedCustomBlacklist` is a filtered view of `CustomBlacklist` that hides
  entries whose EdidId is already in `BuiltInBlacklist`. The underlying
  `CustomBlacklist` (and thus `Properties.MonitorBlacklist`) keeps the original
  entries — they just don't show in the UI. This honors "allow duplicates in
  persistence" without UI noise.

### 4.6 Localization

New resource keys in
[src/settings-ui/Settings.UI/Strings/en-us/Resources.resw](../../src/settings-ui/Settings.UI/Strings/en-us/Resources.resw):

| Key | Sample English |
| --- | --- |
| `PowerDisplay_MonitorBlacklist.Header` | Monitor blacklist |
| `PowerDisplay_MonitorBlacklist.Description` | Models listed here are skipped entirely — PowerDisplay won't enumerate or probe them. |
| `PowerDisplay_MonitorBlacklist_BuiltInSubheader.Text` | Built-in (managed by PowerToys) |
| `PowerDisplay_MonitorBlacklist_CustomSubheader.Text` | Your entries |
| `PowerDisplay_MonitorBlacklist_AddButton.Content` | Add EDID ID |
| `PowerDisplay_MonitorBlacklist_AddDialog_Title.Text` | Add EDID ID to blacklist |
| `PowerDisplay_MonitorBlacklist_EdidLabel.Text` | EDID ID |
| `PowerDisplay_MonitorBlacklist_CommentsLabel.Text` | Comments (optional) |
| `PowerDisplay_MonitorBlacklist_PrimaryButton.Text` | Add |
| `PowerDisplay_MonitorBlacklist_CloseButton.Text` | Cancel |
| `PowerDisplay_MonitorBlacklist_EditButton.[ToolTipService.ToolTip]` | Edit |
| `PowerDisplay_MonitorBlacklist_DeleteButton.[ToolTipService.ToolTip]` | Delete |
| `PowerDisplay_MonitorBlacklist_Validation_InvalidEdid` | EDID ID must be alphanumeric (1–16 characters). |
| `PowerDisplay_MonitorBlacklist_Validation_DuplicateOfBuiltIn` | This EDID is already in the built-in list. |
| `PowerDisplay_MonitorBlacklist_Validation_DuplicateOfCustom` | This EDID is already in your custom list. |

Built-in entries' `comments` field is **not** localized — it ships in English
and renders as-is. This is consistent with PowerToys' approach to embedded
operational notes (e.g., VCP capability strings).

### 4.7 Telemetry

Extend
[PowerDisplaySettingsTelemetryEvent](../../src/modules/powerdisplay/PowerDisplay/Telemetry/Events/PowerDisplaySettingsTelemetryEvent.cs)
with a single integer field:

```csharp
public int MonitorBlacklistCount { get; set; }
```

Reports the number of **custom** entries (built-in count is implicit per
release). We do **not** report the EdidIds themselves; a vendor+product code
plus a sufficiently uncommon model can theoretically narrow down a user's
hardware setup.

### 4.8 Settings Reload Flow

No new IPC. Reuses the existing path:

1. Settings UI mutates `Properties.MonitorBlacklist` and saves via
   `SettingsUtils.SaveSettings`.
2. Runner picks up the change and forwards via the existing named pipe to the
   PowerDisplay app.
3. PowerDisplay receives the "settings changed" notification and calls
   `RefreshMonitorsAsync` — same path as today's `MaxCompatibilityMode`
   propagation
   ([MainViewModel.Monitors.cs:113](../../src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs#L113)).
4. `MonitorManager` rebuilds its `MonitorBlacklistService` and re-runs
   discovery; newly blacklisted monitors disappear from the list, newly
   unblacklisted ones reappear.

Stale `MonitorInfo` entries for now-blacklisted monitors that remain in
`Properties.Monitors` age out via the existing 30-day retention
([PowerDisplaySettings.cs:23](../../src/settings-ui/Settings.UI.Library/PowerDisplaySettings.cs#L23)).
No active cleanup required.

### 4.9 Backward Compatibility

- Old `settings.json` files without a `monitor_blacklist` key deserialize to an
  empty list (default constructor behavior).
- Old PowerToys binaries reading a `settings.json` that **contains**
  `monitor_blacklist` simply ignore the unknown property (System.Text.Json
  default behavior).
- `PowerDisplaySettings.Version` stays at `"1"`.

## 5. Testing

New test files in `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/`:

### 5.1 `BuiltInMonitorBlacklistTests.cs`

- Embedded resource loads successfully and produces a non-null list.
- Entries are normalized to upper-case on load.
- Unknown JSON fields are ignored.
- Empty `entries` array yields empty list.
- Malformed JSON (simulate via test double / private overload) → loader logs
  and returns empty list, never throws.

### 5.2 `MonitorBlacklistServiceTests.cs`

- Empty built-in + empty custom → `IsBlocked` is false for any input.
- Built-in-only match.
- Custom-only match.
- Match present in both — still hits once, no duplication semantics.
- Case-insensitive match: `deld1a8` in entry vs `DELD1A8` extracted from
  device path → hit.
- Trimmed-whitespace match: entry `"  DELD1A8  "` → normalized → hit.
- Empty / `Unknown` EdidId extracted from a degenerate device path → `IsBlocked`
  returns false (we never block monitors we can't identify).
- Full device path containing the EdidId as a substring elsewhere does not
  cause false positives — extraction is via `EdidIdFromMonitorId` only.

### 5.3 Settings UI / ViewModel tests

Sketch (filed under the existing Settings.UI test project pattern if one
exists; otherwise smoke-tested manually):

- Add flow with valid input → entry appears in `CustomBlacklist`,
  `Properties.MonitorBlacklist` updated, `PropertyChanged` fires.
- Add flow with invalid input → entry not added, validation message shown,
  primary button disabled.
- Add flow with EdidId already in built-in → entry **is** added (per policy)
  but does **not** appear in `DisplayedCustomBlacklist`.
- Delete flow → entry removed.
- Edit flow → entry updated, identity preserved (UI doesn't lose scroll
  position).

### 5.4 Integration smoke (manual)

1. Run Settings UI, edit a connected monitor's EdidId into the custom list.
2. Observe: monitor disappears from the Settings monitor list and the
   PowerDisplay flyout within a refresh cycle.
3. Remove the entry → monitor reappears, all its persisted settings
   (brightness toggles, etc.) intact.

## 6. Files Touched (preview)

New:

- `src/modules/powerdisplay/PowerDisplay.Models/MonitorBlacklistEntry.cs`
- `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.cs`
- `src/modules/powerdisplay/PowerDisplay.Models/BuiltInMonitorBlacklist.json`
- `src/modules/powerdisplay/PowerDisplay.Lib/Services/MonitorBlacklistService.cs`
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/BuiltInMonitorBlacklistTests.cs`
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MonitorBlacklistServiceTests.cs`

Modified:

- `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplay.Models.csproj` —
  EmbeddedResource entry
- `src/modules/powerdisplay/PowerDisplay.Models/ProfileSerializationContext.cs` —
  add `MonitorBlacklistEntry` / list types to source-gen context (or a new
  serialization context if cleaner)
- `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs` — new
  `MonitorBlacklist` field
- `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/MonitorDiscoveryHelper.cs`
  (or `MonitorManager`) — call into the gateway before probing
- `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` — new
  collections, add/edit/delete commands, displayed-view filter
- `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml` — new
  expander under Advanced settings
- `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs` —
  click handlers for add/edit/delete (matching CustomVcpMappings style)
- `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` — new keys
- `src/modules/powerdisplay/PowerDisplay/Telemetry/Events/PowerDisplaySettingsTelemetryEvent.cs` —
  `MonitorBlacklistCount` field

No installer / WiX changes (embedded resource is bundled with the dll).
