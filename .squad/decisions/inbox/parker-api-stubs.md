# Decision: Raycast API Stub Architecture

**Author:** Parker (Core/SDK Dev)
**Date:** 2026-03-04
**Status:** Implemented (spike)
**Scope:** `src/modules/cmdpal/extensionsdk/raycast-compat/src/api-stubs/`

## Context

Raycast extensions import non-UI APIs from `@raycast/api` — things like `showToast`, `Clipboard`, `LocalStorage`, `environment`, `getPreferenceValues`, `Icon`, `Color`, etc. For the compat shim to work, these need to exist and not crash when called.

## Key Decisions

### 1. One-file-per-category, barrel export
Each Raycast API category gets its own file. Extensions import from the barrel (`index.ts`). This keeps the codebase navigable and lets us swap real implementations in later without touching other stubs.

### 2. Bootstrap via internal functions
`_configureEnvironment()`, `_setStoragePath()`, `_setPreferencesPath()` are called by the compat runtime before user extension code runs. This separates "extension-facing API" from "runtime wiring." The underscore prefix convention signals internal-only.

### 3. LocalStorage is file-backed JSON
Simplest possible persistence that survives process restarts. Located at `<supportPath>/local-storage.json`. In-memory cache avoids repeated disk reads. Future optimization: batch writes with debounce if perf becomes an issue.

### 4. Icon mapping: Segoe MDL2 + emoji
Raycast icons are named constants. We map ~170 to Windows' Segoe MDL2 Assets glyphs or emoji. Unknown icons get a generic fallback. This provides visual parity without requiring icon assets.

### 5. AI features throw, not silently fail
Extensions using `AI.ask()` get an immediate, clear error. This is better than returning empty strings — it tells the extension developer exactly what's unsupported, and lets the extension's own error handling take over.

### 6. Hooks are real React hooks
`useCachedPromise` and `useFetch` use actual `useState`/`useEffect` so they work inside the reconciler. This is important because Ash's reconciler captures the React tree — if hooks are no-ops, state-driven renders wouldn't work.

### 7. Navigation is stub-only (for now)
`closeMainWindow()`, `popToRoot()`, `launchCommand()` are console stubs. CmdPal's navigation model is declarative (via `CommandResult`), and the full bridge between imperative Raycast navigation and declarative CmdPal results is a separate task.

## Impact on Team

- **Ash (Reconciler):** hooks.ts exports are real React hooks — they'll work inside the reconciler tree.
- **Runtime integration (future):** The `_configure*` bootstrap functions define the contract between the compat runtime and these stubs.
- **Manifest translator:** Preferences flow: manifest translator writes `raycast-compat.json` → installer populates `preferences.json` → `getPreferenceValues()` reads it.

## Test Coverage

28 tests across all stub categories. Run: `cd src/modules/cmdpal/extensionsdk/raycast-compat && npx jest`
