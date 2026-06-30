# PowerDisplay — Per-Feature VCP Code Resolution & Persistence

Design spec. Date: 2026-06-30. Branch: `yuleng/powerdisplay/vcp-code-resolution/1`.

## 1. Problem

Each PowerDisplay feature (brightness, contrast, volume, color temperature, input
source, power state) is currently bound to **exactly one hardcoded VCP code**
(`NativeConstants.cs`: brightness `0x10`, contrast `0x12`, volume `0x62`, color
preset `0x14`, input source `0x60`, power mode `0xD6`). `DdcCiController` uses these
constants verbatim in its `Get*/Set*/Initialize*` methods.

In reality a single logical feature can map to **different VCP codes on different
monitors**. The canonical case is brightness: most monitors expose it on `0x10`
(Luminance), but some expose only `0x13` (Backlight Control) or `0x6B`
(Backlight Level: White). With a single hardcoded code these monitors appear to not
support the feature.

We need a mechanism where, for each feature, we define an **ordered list of candidate
VCP codes by priority**, then **resolve** which code a given monitor actually uses,
**persist** that decision per monitor (so we never re-probe needlessly — including a
"checked, none supported" sentinel), and **surface** the resolved code in the
diagnostics output.

## 2. Goals / Non-Goals

### Goals
- A general, data-driven registry mapping each feature to an ordered candidate-code list.
- A pure, unit-testable resolver implementing **cap-string-first, probe-as-fallback**.
- Per-monitor persistence of the resolved map (including a "not supported" sentinel),
  reused across sessions and invalidated **only** by a user-initiated Refresh.
- `DdcCiController` reads/writes each feature through its resolved code.
- Resolved codes appear in the per-monitor diagnostics ("Copy Diagnostics").

### Non-Goals
- Changing native P/Invoke signatures (`GetVCPFeatureAndVCPFeatureReply`, `SetVCPFeature`).
- Reworking discrete features' value enumeration (input-source / power-state value
  lists still come from the capabilities string).
- A user-facing per-monitor code-override UI (out of scope; the registry is the source
  of candidate lists).
- Seeding alternate codes for features other than brightness. The mechanism is general;
  only brightness is seeded with multiple candidates now (decision: "general framework,
  brightness-only multi-candidate"). All other features keep a single-candidate list
  equal to their current constant.

## 3. Key Decisions (confirmed)

1. **Resolution model = cap-string-first, probe-as-fallback.**
   - Normal mode: resolve purely from the parsed capabilities string, in priority order.
   - Max-compatibility mode: same Phase 1; **only if** the cap string lists none of a
     feature's candidates do we actively probe candidates (read-only `GetVCP`) in
     priority order and take the first that responds with a usable value.
2. **Brightness candidate priority = `0x10` → `0x13` → `0x6B`.** Other features:
   single-candidate (their existing constant).
3. **Invalidation = user Refresh only.** A persisted map (code or sentinel) is reused
   indefinitely; the user-initiated Refresh clears it and forces re-resolution.
   Startup and the display-change watcher reuse persisted maps without clearing.
   - Documented consequence: a feature resolved to "not supported" via cap-string-only
     (max-compat off) will **not** be auto-re-probed when max-compat is later turned on;
     the user must press Refresh. This is the intended trade-off.
4. **Shape = general framework, brightness-only multi-candidate** (see Non-Goals).

## 4. Architecture

```
                 PowerDisplay (app)                         PowerDisplay.Lib
  MainViewModel ── reads/writes persisted maps ──► MonitorStateManager (state file)
       │  push persisted maps + maxCompat                       ▲  persist resolved maps
       ▼  (before discovery)                                    │
  MonitorManager ──────────────────────────────►  DdcCiController
                                                      │ BuildMonitorFromPhysical
                                                      │   caps + handle
                                                      ▼
                                                   VcpFeatureResolver.Resolve(
                                                      caps, maxCompat, persistedMap, probe)
                                                      │ uses VcpFeatureRegistry
                                                      ▼
                                                   Monitor.ResolvedVcpCodes (VcpFeatureCodeMap)
                                                      │
  CreateMonitorInfo ──► MonitorInfo.ResolvedVcpCodes ─┘ (settings.json → Settings UI)
                              │
                              ▼
                        MonitorInfo.GetDiagnosticsAsText()  (hex-only section)
```

Resolution runs **inside the discovery pipeline** so each feature's current value is
initialized through the correct code in a single pass. The persisted map and the
max-compat flag are pushed into the controller before discovery (mirroring the existing
`SetMaxCompatibilityMode`). The resolver itself is a pure function (no I/O, no
persistence) and is fully unit-testable with a fake `probe` delegate.

## 5. New types (`PowerDisplay.Lib`)

### 5.1 `VcpFeature` (enum)
`Brightness, Contrast, Volume, ColorTemperature, InputSource, PowerState`.
Each has a stable persistence/diagnostic key:
`brightness, contrast, volume, colorTemperature, inputSource, powerState`.

### 5.2 `VcpFeatureRegistry` (static)
Ordered candidate codes per feature (composed from `NativeConstants` literals, which
remain the single source of truth for the values):

| Feature          | Candidates (priority order)         |
|------------------|-------------------------------------|
| Brightness       | `0x10` → `0x13` → `0x6B`             |
| Contrast         | `0x12`                              |
| Volume           | `0x62`                              |
| ColorTemperature | `0x14`                              |
| InputSource      | `0x60`                              |
| PowerState       | `0xD6`                              |

API: `IReadOnlyList<byte> Candidates(VcpFeature)`, `byte Primary(VcpFeature)` (= first
candidate, used as a safe default), `IReadOnlyList<VcpFeature> AllFeatures`, plus
`string Key(VcpFeature)` / `bool TryParseKey(string, out VcpFeature)`.

### 5.3 `VcpFeatureCodeMap`
Per-monitor resolved result. Internally `Dictionary<VcpFeature, int>`:
- value `0x00`–`0xFF` → resolved code,
- value `NotSupportedSentinel` (`= -1`, named constant) → checked, no candidate works,
- key absent → that feature not resolved yet.

The `-1` sentinel is required (not laziness): the monitor-state file serializes with
`DefaultIgnoreCondition = WhenWritingNull`, which would silently drop a `null`-means-
unsupported encoding and erase the distinction between "checked-unsupported" and
"never-resolved". `-1` cannot collide with a real byte code.

API:
- `int GetCode(VcpFeature)` → resolved code if supported, else `VcpFeatureRegistry.Primary`
  (callers gate on `IsSupported`; primary is a safe fallback so a byte is always returned).
- `bool IsSupported(VcpFeature)` → has entry and entry != sentinel.
- `bool IsResolved(VcpFeature)` → has any entry (code or sentinel).
- `Dictionary<string,int> ToPersisted()` / `static VcpFeatureCodeMap FromPersisted(Dictionary<string,int>?)`
  (string keys via `VcpFeatureRegistry.Key`; unknown keys ignored for forward-compat).

### 5.4 `VcpFeatureResolver` (static, pure)
```csharp
static VcpFeatureCodeMap Resolve(
    VcpCapabilities caps,
    bool maxCompatibilityMode,
    VcpFeatureCodeMap? persisted,                 // per-feature reuse source
    Func<byte, bool> probe);                      // read-only GetVCP, returns true iff usable
```
Algorithm, per feature in `VcpFeatureRegistry.AllFeatures`:
1. **Per-feature reuse.** If `persisted` has an entry (code or sentinel) for this
   feature → copy it verbatim (no cap-string lookup, no probe). This makes reuse
   forward-compatible: a persisted map missing a newly-added feature resolves only the
   missing one.
2. Otherwise resolve fresh:
   1. **Phase 1 (both modes).** First candidate with `caps.SupportsVcpCode(candidate)` → code.
   2. **Phase 2 (max-compat only, if Phase 1 found nothing).** First candidate where
      `probe(candidate)` returns true → code.
   3. Else → `NotSupportedSentinel`.

`probe` is supplied by the controller and encodes **usability**, not mere call success:
`code => TryGetVcpFeature(handle, code, out cur, out max) && max > 0` — so brightness
never resolves to an advertised-but-dead code (aligns with the existing
`InitializeBrightness` range guard). Normal mode never invokes `probe`.

## 6. Controller changes (`DdcCiController`)

- Add `IReadOnlyDictionary<string, VcpFeatureCodeMap> PersistedVcpCodeMaps { get; set; }`
  (default empty), pushed before discovery like `MaxCompatibilityMode`.
- In `BuildMonitorFromPhysical`, **immediately after `monitor.VcpCapabilitiesInfo = caps;`
  and before `UpdateMonitorCapabilitiesFromVcp` / the `Initialize*` calls** (ordering is
  load-bearing — both downstream steps now read the resolved map):
  ```csharp
  PersistedVcpCodeMaps.TryGetValue(monitor.Id, out var persisted);
  monitor.ResolvedVcpCodes = VcpFeatureResolver.Resolve(
      caps, MaxCompatibilityMode, persisted,
      code => TryGetVcpFeature(physical.HPhysicalMonitor, code, monitor.Id, out var c, out var m) && m > 0);
  ```
- `UpdateMonitorCapabilitiesFromVcp`: **brightness support only** now derives from the
  resolved map — `if (monitor.ResolvedVcpCodes.IsSupported(VcpFeature.Brightness)) monitor.Capabilities |= Brightness;`.
  Contrast/volume/color-temperature support detection is **unchanged** (still cap-string
  based); those are single-candidate, so behavior is identical and risk is contained.
- `Initialize*` and `Get*/Set*` use `monitor.ResolvedVcpCodes.GetCode(feature)` instead
  of the `NativeConstants` literal. `InitializeBrightness` reads its max via the resolved
  brightness code; percent↔raw scaling (`BrightnessVcpMax`) is unchanged. For single-
  candidate features the resolved code equals the old constant, so reads/writes are
  byte-for-byte identical to today.

`Monitor` (model) gains `public VcpFeatureCodeMap ResolvedVcpCodes { get; set; } = new();`.

## 7. Persistence (`MonitorStateEntry` + `MonitorStateManager`)

- `MonitorStateEntry` gains
  `[JsonPropertyName("vcpFeatureCodes")] Dictionary<string,int>? VcpFeatureCodes`
  (null when never resolved; values include the `-1` sentinel). Register
  `Dictionary<string,int>` in `MonitorStateSerializationContext`.
- `MonitorStateManager` internal `MonitorState` mirrors the field; `LoadStateFromDisk`,
  `BuildStateJson`, `CloneState` carry it through. New API:
  - `VcpFeatureCodeMap? GetVcpCodeMap(string monitorId)`
  - `void UpdateVcpCodeMap(string monitorId, VcpFeatureCodeMap map)` — dirty-flag +
    debounced save; **skips write if the persisted dict is unchanged** (idempotent).
  - `void ClearAllVcpCodeMaps()` — nulls only `VcpFeatureCodes` on every entry (brightness/
    contrast/volume/color values are preserved), marks dirty, schedules save.

Example `monitor_state.json` entry:
```json
"\\\\?\\DISPLAY#DELD1A8#5&abc&0&UID1": {
  "brightness": 75,
  "vcpFeatureCodes": { "brightness": 107, "contrast": 18, "volume": -1,
                       "colorTemperature": 20, "inputSource": 96, "powerState": 214 },
  "lastUpdated": "2026-06-30T10:30:45.1"
}
```
(`107`=`0x6B`, `18`=`0x12`, `-1`=not supported, `20`=`0x14`, `96`=`0x60`, `214`=`0xD6`.)

## 8. App orchestration (`MainViewModel`)

- **Before discovery** (`InitializeAsync` and `RefreshMonitorsAsync`): build
  `Dictionary<string, VcpFeatureCodeMap>` from `_stateManager` for all known monitors and
  push via `_monitorManager.SetPersistedVcpCodeMaps(maps)` (new pass-through to the DDC
  controller), alongside the existing `SetMaxCompatibilityMode`.
- **After discovery** (in `UpdateMonitorList`, where `SaveMonitorsToSettings` already
  runs): for each discovered monitor call
  `_stateManager.UpdateVcpCodeMap(monitor.Id, monitor.ResolvedVcpCodes)` (idempotent).
- **Refresh clears**: `RefreshMonitorsAsync` calls `_stateManager.ClearAllVcpCodeMaps()`
  and pushes empty maps **before** re-discovery, forcing full re-resolution. Startup and
  the watcher path do **not** clear.
  - The "Refresh" the user means is the existing user-initiated refresh command that calls
    `RefreshMonitorsAsync` (confirm the exact command/button binding during implementation;
    the display-change-watcher entry must remain on the reuse path).

## 9. Diagnostics (`MonitorInfo` + `CreateMonitorInfo`)

- `MonitorInfo` (Settings.UI.Library) gains
  `[JsonPropertyName("resolvedVcpCodes")] Dictionary<string,int> ResolvedVcpCodes`
  and copies it in `UpdateFrom`. Register the dict type in the settings source-gen context.
- App `CreateMonitorInfo` populates it from `monitor.ResolvedVcpCodes.ToPersisted()`.
- `GetDiagnosticsAsText()` inserts a section between "Detected support" and
  "Raw capabilities":
  ```
  Resolved feature -> VCP code
  --------------------------------------------------
  Brightness: 0x6B
  Contrast: 0x12
  Volume: not supported
  ColorTemperature: 0x14
  InputSource: 0x60
  PowerState: 0xD6
  ```
  **Hex-only, no friendly names.** `GetDiagnosticsAsText` lives in
  `Settings.UI.Library`, which must not take a binary dependency on `PowerDisplay.Lib`
  (where `VcpNames` lives) — that pattern has crashed root apps before. Code names are
  still available to the reader: the existing `GetVcpCodesAsText()` block below already
  lists every detected code with its name. Sentinel renders `not supported`; an absent
  entry renders `not resolved`.

## 10. Tests (`PowerDisplay.Lib.UnitTests`, MSTest + Moq)

- `VcpFeatureResolverTests`:
  - Phase-1 priority: caps with `0x10` → brightness `0x10`; caps with only `0x6B` →
    brightness `0x6B`; caps with `0x13`+`0x6B` → `0x13` (priority).
  - Normal mode never probes: assert the `probe` delegate is invoked zero times.
  - Max-compat probe fallback: caps lists no brightness candidate, `probe(0x6B)` true →
    `0x6B`; assert probe call order honors priority and stops at first success.
  - Probe usability: `probe` returns false when `max == 0` → not chosen.
  - All-miss → sentinel.
  - Per-feature reuse: a persisted entry is copied verbatim and `probe`/caps are not
    consulted for that feature; a partially-populated persisted map resolves only the
    missing features.
- `VcpFeatureCodeMapTests`: `ToPersisted`/`FromPersisted` round-trip, sentinel semantics,
  unknown-key tolerance, `GetCode` fallback to `Primary`.
- `MonitorStateManager` round-trip test extended to assert `vcpFeatureCodes` survives
  save/load and `ClearAllVcpCodeMaps` preserves the value fields.

## 11. Edge cases & notes

- **Cap-string totally unreadable + max-compat**: existing `ProbeSupportedVcpFeatures`
  reconstructs a synthetic caps object from `{0x10,0x12,0x62}` only. Our resolver then
  runs on that synthetic caps; brightness alternates (`0x13`,`0x6B`) are found via our
  Phase-2 probe. This causes at most 1–2 redundant `GetVCP` reads on `0x10` in that rare
  path — accepted, not optimized.
- **Discrete features**: `GetCode` returns the standard code; value lists
  (`SupportedInputSources`, color presets) still come from `caps`. The resolved map only
  governs which code we read/write.
- **Forward/backward compat**: new JSON fields are additive. Old state/settings files
  lack them → treated as "never resolved" → resolved on next discovery. New files read by
  an older build → unknown fields ignored.
- **Concurrency**: discovery resolves per-monitor concurrently; the pushed persisted map
  and static registry are read-only during discovery; write-back is single-threaded in the
  app. No shared mutable state.
- **No new external dependencies**; no ABI/native-signature changes; settings/IPC schema
  only gains additive fields.

## 12. Out-of-scope follow-ups (not in this change)
- Friendly code names in the diagnostics "Resolved" section (would need a shared
  code-name table in `PowerDisplay.Models`, referenced by both Lib and Settings.UI.Library).
- Seeding multi-candidate lists for non-brightness features (extend `VcpFeatureRegistry`).
- A per-monitor manual code-override UI.
