# Migrate Workspaces Editor from WPF to WinUI 3

## Background

The Workspaces Launcher UI has been successfully migrated from WPF to WinUI 3 ([PR #48700](https://github.com/microsoft/PowerToys/pull/48700)), establishing reusable patterns for the codebase. The Workspaces Editor is the remaining WPF-based UI surface in the Workspaces module and represents a larger, more complex migration effort.

The Editor is the primary user-facing window for creating, editing, and managing workspaces. It includes multiple pages, complex data templates, and COM interop for shortcut creation.

---

## Goal

Migrate the Workspaces Editor from WPF to WinUI 3 to:

- Complete the Workspaces module WinUI modernization
- Remove all WPF dependencies from the Workspaces module
- Maintain feature parity with existing Editor functionality
- Leverage patterns established in the Launcher UI migration
- Improve long-term maintainability and UI consistency

## Non-Goals

The following are explicitly out of scope:

- New user-facing features or UX redesigns
- Changes to workspace configuration format (`workspaces.json`)
- Changes to the C++ engine components (Launcher, WindowArranger, SnapshotTool)
- Changes to the Module Interface
- Telemetry changes

> The objective is functional parity, not feature expansion.

---

## Scope

### In Scope

- WorkspacesEditor WPF application (6 XAML files, 31 C# files)
- WorkspacesCsharpLibrary WPF imaging code (`BaseApplication.cs` icon handling)
- Resource dictionaries and styling
- ViewModels and data binding
- Accessibility and theme support
- Installer and signing updates
- WorkspacesEditorUITest updates

### Out of Scope

- Workspaces core C++ functionality
- Launcher UI (already migrated to WinUI 3)
- Named pipe IPC protocol
- Window placement algorithms
- Configuration file format changes

### Dependencies (Must Be Resolved First)

> **Blocker:** `WorkspacesCsharpLibrary` contains WPF imaging code (`System.Windows.Media.Imaging.BitmapImage`) used by both the Editor and `Workspaces.ModuleServices`. This library must be updated to remove WPF dependencies before the Editor migration can proceed.

---

## Key Challenges

This migration is significantly more complex than the Launcher UI:

| Challenge | Details | Approach |
|-----------|---------|----------|
| Multiple windows/pages | MainWindow, WorkspacesEditorPage, SnapshotWindow, OverlayWindow | Migrate each window independently, starting from leaf pages |
| Frame-based navigation | WPF `Frame` + `Page` pattern | WinUI `NavigationView` or direct content switching |
| WPF Triggers in multiple locations | `Style.Triggers`, `DataTriggers` on IsEnabled, IsMouseOver | Convert each to `VisualStateManager` states |
| Expander with complex DataTemplates | Workspace app list uses `Expander` with nested templates | WinUI `Expander` is a direct equivalent; port DataTemplate content |
| COM interop (shortcut creation) | `IWshRuntimeLibrary` for Windows shortcuts | COM interop works identically in WinUI; no migration needed |
| BitmapImage in shared library | `WorkspacesCsharpLibrary.BaseApplication` uses WPF imaging | Replace with `Microsoft.UI.Xaml.Media.Imaging.BitmapImage` or `Windows.Graphics.Imaging` |
| Icon extraction (GDI+ pipeline) | `System.Drawing.Icon` → `Bitmap` → `BitmapImage` chain | Replace with `Windows.Graphics.Imaging.SoftwareBitmap` pipeline |

---

## Risks to Investigate Before Writing Code

These areas require spikes before committing to implementation:

### 1. SnapshotWindow & OverlayWindow (HIGH RISK)

The capture experience relies on:
- Transparent windows
- Topmost behavior
- Screen coordinates and hit testing
- Desktop overlay rendering

WinUI has known gaps in windowing and overlay scenarios that often require `AppWindow`/HWND interop. This is where functional regressions are most likely to surface.

**Action:** Spike a minimal transparent topmost WinUI window with click-through behavior before estimating Milestone 4.

### 2. Resource Migration (MEDIUM RISK)

The `.resx` → `.resw` migration is straightforward per-file but touches nearly every XAML file. Before estimating effort, inventory:
- Total number of localized strings
- Converters that reference resource strings
- Bindings that depend on `{x:Static}` resource syntax

**Action:** Run a count of `x:Static props:Resources.` references across all Editor XAML files.

### 3. UITest Migration (MEDIUM RISK)

UI test migration effort is often underestimated:

> UI migration = 40% of effort, Test fixes = 60% of effort

`WorkspacesEditorUITest` may depend on:
- WPF-specific element identifiers
- Accessibility IDs that change with WinUI
- Automation patterns that differ between frameworks

**Action:** Inspect `WorkspacesEditorUITest` early to understand element identifiers, accessibility IDs, and automation patterns before assuming they port cleanly.

---

## PR Structure

**Single PR with 5 milestones** (same pattern as the Launcher UI migration):

### Milestone 1: Remove WPF Imaging Dependencies

**Goal:** Decouple `WorkspacesCsharpLibrary` from WPF-specific imaging APIs.

- [ ] Remove `System.Windows.Media.Imaging` dependency from `BaseApplication.cs`
- [ ] Replace WPF `BitmapImage` property with WinUI-compatible alternative
- [ ] Update icon extraction pipeline (GDI+ → SoftwareBitmap or platform-agnostic)
- [ ] Verify `Workspaces.ModuleServices` still builds and functions
- [ ] Run existing tests to confirm no regressions

**Success criteria:** No `System.Windows.*` imaging dependencies remain. Existing tests pass. Editor still builds.

**Why first?** This is the primary blocker — the Editor and ModuleServices both depend on this library.

---

### Milestone 2: WinUI Editor Foundation

**Goal:** Create the new WinUI editor project and bootstrapping infrastructure.

- [ ] Create new WinUI `.csproj` (`WorkspacesEditor.WinUI`)
- [ ] Custom entry point with `DISABLE_XAML_GENERATED_MAIN`
- [ ] GPO check and singleton mutex (match Launcher UI pattern)
- [ ] `DispatcherQueue` setup
- [ ] Create empty MainWindow shell
- [ ] Verify project builds and window displays

**Success criteria:** Empty editor launches successfully. Existing functionality untouched.

---

### Milestone 3: Main Editor Page Migration

**Goal:** Move the primary workspace management experience to WinUI.

This is likely the largest milestone.

- [ ] Port the main editor page layout (workspace list, search, sort, create button)
- [ ] Migrate DataTemplates (workspace items with app lists)
- [ ] Convert `Expander` controls (WPF Expander → WinUI Expander)
- [ ] Port `Style.Triggers` → `VisualStateManager`
- [ ] Wire ViewModel data binding (`ObservableCollection`, `INotifyPropertyChanged`)
- [ ] Migrate `.resx` strings → `.resw` with `x:Uid` pattern

**Success criteria:** Workspace list renders. Search works. Sort works. Workspace selection works.

---

### Milestone 4: Snapshot + Overlay Migration

**Goal:** Move the workspace capture experience to WinUI.

This is where most functional regressions will likely surface due to WinUI windowing limitations.

- [ ] Port SnapshotWindow (capture overlay)
- [ ] Port OverlayWindow (desktop overlay during capture)
- [ ] Wire navigation flow: Editor → Snapshot → return to Editor
- [ ] Handle transparent/topmost window behavior via `AppWindow`/HWND interop

**Success criteria:** New workspace creation works end-to-end. Capture flow works. Return-to-editor flow works.

---

### Milestone 5: Final Integration & WPF Removal

**Goal:** Complete migration and remove legacy implementation.

- [ ] Remove old WPF `WorkspacesEditor` project
- [ ] Update installer references (WiX, signing)
- [ ] Update solution file (`PowerToys.slnx`)
- [ ] Update verification script paths
- [ ] Update `WorkspacesEditorUITest` to reference new project
- [ ] Accessibility validation (keyboard nav, Narrator, High Contrast)
- [ ] Theme validation (Light/Dark/HC)
- [ ] Final test pass

**Success criteria:** All existing scenarios pass. WPF editor removed. WinUI editor becomes production implementation.

---

## Validation

### Functional Testing

- [ ] Create new workspace (capture, name, save)
- [ ] Edit workspace (rename, remove apps, modify positions)
- [ ] Launch workspace from Editor
- [ ] Delete workspace
- [ ] Search workspaces by name/app
- [ ] Sort workspaces (name, created, last launched)
- [ ] Create desktop shortcut for workspace
- [ ] Launch & Edit flow (re-capture)

### Accessibility Testing

- [ ] Keyboard-only navigation through all Editor controls
- [ ] Tab order logical and consistent
- [ ] Narrator announces all interactive elements
- [ ] Focus management after dialogs and page transitions
- [ ] High Contrast mode renders correctly

### Visual Testing

- [ ] 100%, 150%, 200% DPI scaling
- [ ] Light Theme
- [ ] Dark Theme
- [ ] Multiple monitor environments
- [ ] Window resizing behavior

---

## Expected Outcome

The Workspaces module will be completely free of WPF dependencies. Both the Editor and Launcher UI will run on WinUI 3, providing a consistent Fluent UI experience. The patterns established in the Launcher UI migration (project structure, IPC handling, resource management, accessibility approach) will be directly reusable, reducing the learning curve for this larger effort.

**Estimated effort:** 30–40 hours, single PR with 5 milestones.
