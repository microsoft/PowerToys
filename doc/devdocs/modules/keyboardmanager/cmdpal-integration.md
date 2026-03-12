# Keyboard Manager CmdPal Integration

## Goal

Expose Keyboard Manager mappings in Command Palette (`cmdpal`) through `ext.powertoys` with two separate user experiences:

- quick actions for executable mappings
- an inspection list for all current mappings

This should be done without introducing a new settings schema or a CmdPal-specific Keyboard Manager settings file.

The first scope should cover:

- Expose `Run Program` remaps as invokable CmdPal actions
- Expose `Open URI` remaps as invokable CmdPal actions
- Add one Keyboard Manager `List all mappings` command item
- List all current mappings on a dedicated Keyboard Manager page
- Make the primary interaction for a mapping item be inspection of what that mapping does

## Current State

The repository already contains most of the plumbing needed for this integration:

1. Keyboard Manager publishes actions through the module action surface in `src/modules/keyboardmanager/dll/dllmain.cpp`.
2. Runner aggregates module actions through `src/runner/action_registry.cpp` and exposes them over the existing named pipe consumed by `RunnerActionClient`.
3. The PowerToys CmdPal extension enumerates module commands through `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Helpers/ModuleCommandCatalog.cs`.
4. Keyboard Manager already has a provider in `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Modules/KeyboardManagerModuleCommandProvider.cs` that:
   - shows the active-state toggle
   - opens the new editor
   - enumerates Runner actions with the `powertoys.keyboardManager.mapping.` prefix
   - invokes them through `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Commands/KeyboardManager/InvokeKeyboardManagerCustomActionCommand.cs`

At the Keyboard Manager layer, only remappings backed by executable actions are currently turned into invokable actions:

- `Shortcut::IsRunProgram()`
- `Shortcut::IsOpenURI()`

This is implemented in `is_keyboard_manager_custom_action`, `append_mapping_actions`, and `invoke_keyboard_manager_custom_action` in `src/modules/keyboardmanager/dll/dllmain.cpp`.

The repository also already has read-side Keyboard Manager mapping logic in `src/modules/keyboardmanager/KeyboardManagerEditorUI/Interop/KeyboardMappingService.cs`, but that code lives inside the editor UI project and is not an appropriate dependency for `ext.powertoys`.

## Lightweight Design

### Design Decision

Split the design into two surfaces:

1. Executable mapping actions
2. Mapping inspection

Executable mapping actions stay centered on the existing Runner action registry.

Mapping inspection should use a dedicated read-only Keyboard Manager query service shared with CmdPal, following the existing `*.ModuleServices` pattern used by other modules.

Do not add:

- a new CmdPal-only data file
- a direct JSON parse of Keyboard Manager settings in `ext.powertoys`
- a UI-project dependency from `ext.powertoys` to `KeyboardManagerEditorUI`
- CmdPal-specific logic in the Keyboard Manager editor

### Why This Is The Right Shape

This matches the way other PowerToys modules integrate with CmdPal:

- module owns its state and execution semantics
- executable actions are published through a small action surface
- richer read-only data can be exposed through a shared service layer when the module needs inspection or navigation

This keeps the startup path lean and avoids duplicating Keyboard Manager parsing logic in `ext.powertoys`.

## Proposed Functional Model

### Surface A: Executable Actions

Keyboard Manager remains the source of truth for which mappings are eligible for direct invocation in CmdPal.

When `get_actions()` is called:

1. Load the current `MappingConfiguration`
2. Enumerate OS-level shortcut remaps
3. Enumerate app-specific shortcut remaps
4. Keep only remaps whose target operation is:
   - `Run Program`
   - `Open URI`
5. Emit one Runner action descriptor per eligible remap

### Identity For Executable Actions

Each action id remains derived from the remap identity:

- source shortcut
- exact-match flag
- app scope
- target operation type
- target payload fields

The current implementation uses a hashed identity under the prefix `powertoys.keyboardManager.mapping.`. That is acceptable for a lightweight design because:

- CmdPal does not need stable ids across edits beyond the current session
- the action id is regenerated from source-of-truth settings
- action invocation already re-resolves the action against current config and fails safely if the mapping no longer exists

### Invocation Of Executable Actions

CmdPal invokes the selected item through `RunnerActionClient.InvokeAction(actionId)`.

Keyboard Manager stays responsible for:

- launching programs
- open-existing-instance behavior
- elevation mode
- start-in directory
- window visibility
- URI/path normalization and shell execution

This is important because CmdPal should not duplicate Keyboard Manager's execution semantics.

### Surface B: All Mappings Inspection

CmdPal also needs one dedicated Keyboard Manager entry for inspecting every current mapping, not just executable ones.

That entry should be a top-level module item such as:

- `List Keyboard Manager mappings`

Its command should open a dedicated `KeyboardManagerMappingsPage`.

### Data Source For All Mappings

The `KeyboardManagerMappingsPage` should not be backed by Runner actions because Runner actions currently model invokable operations only.

Instead, add a small shared Keyboard Manager query layer, ideally as a module service project, for example:

- `src/modules/keyboardmanager/KeyboardManager.ModuleServices`

That shared service should reuse the existing native mapping query path already used by the editor and expose normalized read-only DTOs for CmdPal consumption.

The service should cover all current mapping categories:

- single key to key
- single key to shortcut
- single key to text
- shortcut to shortcut
- shortcut to program
- shortcut to URI
- app-specific shortcut mappings

### Interaction Model For The Mappings Page

The mappings page should behave like an inspection page first, not an execution page first.

Recommended interaction:

1. `KeyboardManagerMappingsPage` is a `DynamicListPage` or `ListPage` with `ShowDetails = true`
2. Each mapping is rendered as a `ListItem` with rich `Details`
3. Selecting a mapping shows what it maps to
4. Invoking the item opens a small `KeyboardManagerMappingDetailsPage` or equivalent detail-focused page
5. Executable mappings may expose an extra command such as `Run now` or `Open now`, but that should not be the primary action on the inspection page

This satisfies the requirement that the primary action for a mapping entry is to show what the mapping is, while still leaving room for execution when the mapping type supports it.

### Presentation In CmdPal

Within `ext.powertoys`, the Keyboard Manager provider should emit these command groups:

1. Keyboard Manager state commands
   - toggle active state
   - open editor
2. Keyboard Manager inspection commands
   - `List Keyboard Manager mappings`
3. Keyboard Manager quick actions
   - `Run Program` entries
   - `Open URI` entries
4. Keyboard Manager settings
   - open settings

## UX Guidance

The minimum viable experience is:

- searchable by trigger or target
- clearly labeled as Keyboard Manager actions or mappings
- capable of both inspection and direct execution for supported mapping types

Recommended presentation rules:

1. The `List Keyboard Manager mappings` item should use the Keyboard Manager icon and clearly signal it opens a list, not an action.
2. Mapping list titles should prioritize the trigger:
   - `Ctrl+Alt+N`
   - `Caps Lock`
3. Mapping list subtitles should say what the trigger maps to:
   - `Opens notepad.exe`
   - `Maps to Ctrl+C`
   - `Types Hello world`
4. Mapping details should carry the rest of the context:
   - global vs app-specific
   - mapping kind
   - target payload
   - execution-specific options when relevant
5. Quick-action titles can continue to prioritize the executable action:
   - `Run notepad.exe`
   - `Open https://contoso.com`
6. Keyboard Manager module icon is sufficient for the first version

The current implementation already covers the quick-action portion. The new work is primarily the all-mappings inspection surface.

## Non-Goals For The First Version

Do not add these in the initial pass:

- editing Keyboard Manager mappings from CmdPal
- enabling or disabling individual mappings from CmdPal
- live push notifications when mappings change
- custom icons per program or URI
- a new `kbm:` command syntax or dedicated parser

These all increase complexity without being necessary to validate the feature.

## Integration Pattern Compared To Other Modules

This feature now combines two existing CmdPal integration styles.

- `Workspaces` loads module-owned data and emits one command per data item
- `FancyZones` uses dedicated pages and details for richer inspection

Keyboard Manager quick actions should follow the lighter `Workspaces` pattern:

- one provider
- one flat list of dynamic items
- generic command invocation

Keyboard Manager all-mappings inspection should follow the `list page with details` pattern already supported by CmdPal:

- one top-level entry that opens a page
- one list item per mapping
- rich `Details` on every row
- optional secondary commands for invokable mappings

The main constraint is the same in both paths: `ext.powertoys` should not duplicate Keyboard Manager's mapping schema by parsing the settings file directly.

## Error Handling

The existing executable-action behavior is the correct baseline:

- hidden or deleted mappings simply disappear from `list_actions`
- stale CmdPal entries fail through `action_not_found`
- disabled Keyboard Manager returns `module_unavailable`
- launch failures return module-defined error messages

CmdPal only needs to surface the returned message as toast text for quick actions.

For the all-mappings inspection page:

- malformed or unreadable mapping snapshots should yield an empty page or an inline error item
- missing targets should still render as mappings, but be tagged as invalid or unavailable
- details rendering should degrade gracefully when optional fields are absent

## Risks

### Two Data Paths

This design intentionally uses two integration paths:

- Runner actions for invokable mappings
- a shared read-only query service for all mappings

That is acceptable because the two paths serve different UX needs. The risk is manageable as long as both paths derive from the same Keyboard Manager mapping model rather than separate ad hoc parsers.

### Duplicate Or Ambiguous Entries

Different mappings may produce similar titles, especially on the quick-action side. This is acceptable in the first iteration because the subtitle already carries scope and trigger details.

### Action Id Churn After Edits

Editing a mapping changes the derived id. This is acceptable because action ids are not a persisted public contract.

### Large Mapping Sets

Very large mapping sets could make the inspection page noisy. This is manageable for the first version if the page supports search and details, but sectioning or filters may be needed later.

## Minimal Implementation Plan

1. Keep Keyboard Manager as the producer of invokable mapping actions.
2. Keep Runner action registry as the discovery path for executable quick actions.
3. Add a small shared read-only Keyboard Manager module service for enumerating all mappings.
4. Add `List Keyboard Manager mappings` to `KeyboardManagerModuleCommandProvider`.
5. Add a `KeyboardManagerMappingsPage` with `ShowDetails = true`.
6. Represent each mapping as an inspection-first `ListItem` with rich `Details`.
7. Add optional secondary execution commands only for mappings that are invokable.
8. Add focused tests around:
   - Keyboard Manager action enumeration for `Run Program` and `Open URI`
   - mapping snapshot enumeration across all mapping kinds
   - CmdPal rendering of the mappings page
   - graceful handling of stale, invalid, or missing mappings

## Future Extensions

If the first version lands well, the next step should still preserve the same split architecture:

- enrich action descriptors with more metadata if Runner actions grow argument or icon support
- add sections or filters to the mappings page when the list becomes large
- optionally expose app-specific filtering in CmdPal UI

The extension points should remain:

- Runner actions for execution
- a shared Keyboard Manager query service for inspection
