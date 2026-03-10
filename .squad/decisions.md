# Squad Decisions

## Active Decisions

### D-001: Team scoped to Command Palette
- **Date:** 2026-03-10
- **By:** Squad Coordinator
- **Decision:** Team is scoped to the Command Palette module (`src/modules/cmdpal/`). Solution filter: `CommandPalette.slnf`. All work should be within these boundaries plus shared `src/common/` dependencies.
- **Routing:** Trinity → UI/XAML, Morpheus → ViewModels/Services, Tank → Extensions/SDK/C++, Oracle → Tests, Neo → Architecture/Review.

### D-002: Boundary & branching rules
- **Date:** 2026-03-10
- **By:** Neo (directive from project owner)
- **Decision:**
  1. **Project boundary:** We ONLY work on projects listed in `src/modules/cmdpal/CommandPalette.slnf`. No other PowerToys modules may be touched.
  2. **No commits to main:** Never commit directly to the `main` branch. All work must be on a feature branch.
  3. **Branch naming:** `dev/mjolley/{branch-title-here}` — lowercase, hyphen-separated title describing the work.
- **Enforcement:** All agents must check the current branch before committing. If on `main`, create the feature branch first. Scribe and the Coordinator enforce this gate.

### D-003: Base branch is dev/mjolley/persistence
- **Date:** 2026-03-10
- **By:** Neo (directive from project owner)
- **Decision:** All multi-monitor dock work branches from `dev/mjolley/persistence`, NOT `main`. This branch has settings/app-state/personalization persistence changes we depend on. Feature branches for this work: `dev/mjolley/{feature-name}` created from this base.
- **Enforcement:** Before creating a feature branch, confirm current branch is `dev/mjolley/persistence`.

### D-004: SideOverrideIndex mapping for monitor config ComboBox
- **Date:** 2026-03-10
- **By:** Morpheus
- **Decision:** `SideOverrideIndex` uses index-based mapping for ComboBox binding: 0 = "Use default" (`null`), 1 = Left, 2 = Top, 3 = Right, 4 = Bottom. Follows the pattern from `DockBandSettingsViewModel.ShowLabelsIndex` where index mapping enables "inherit" semantics.
- **Enforcement:** Trinity's ComboBox items must match this exact order. Index 0 means inherit from dock-wide `DockSettings.Side`.

### D-005: AOT Discipline for Multi-Monitor Dock Code
- **Date:** 2026-03-10
- **By:** Neo
- **Status:** Applied
- **Decision:** 
  1. **No LINQ in new CmdPal code.** Replace with explicit loops (project is AOT-compiled).
  2. **All classes implementing WinRT interfaces (including IDisposable) must be marked `partial`.**
  3. **Any new list/collection type used in persisted settings must be explicitly registered in `JsonSerializationContext`.**
- **Enforcement:** All agents working on CmdPal must check for LINQ usage and missing `partial` keywords before marking work as done.

### D-006: ViewModels project had pre-existing build errors (RESOLVED)
- **Date:** 2026-03-10
- **From:** Oracle (Tester)
- **Status:** Resolved
- **Issues:** 
  1. **CS0169** in `SettingsViewModel.cs:34` — `_monitorService` field was never used.
  2. **SA1512** in `Dock/DockMonitorConfigViewModel.cs:6` — StyleCop: single-line comment followed by blank line.
- **Resolution:** Morpheus fixed both issues. ViewModels project now builds clean.

### D-007: Trinity fixed missing import in Morpheus's code
- **Date:** 2026-03-10
- **By:** Trinity
- **Context:** `DockMonitorConfigViewModel.cs` was missing `using System.Text;` for `CompositeFormat`, blocking UI build. Added the import as a one-line fix to unblock compilation.
- **Status:** Applied and complete.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
