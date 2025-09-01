# Runner – Copilot guide (short)

Scope
- Tray app that loads modules, manages hotkeys, settings bridge, updates.

Build
- cd to this folder and build the Runner project first (`PowerToys.vcxproj` includes Runner). Use repo scripts if preferred.
- Foreground build only; after it finishes, have Copilot read the build errors log (e.g., `build.*.*.errors.log`).

Tests / checks
- If you changed IPC/JSON, verify the code at `/src/settings-ui/**` for consistency.

Gotchas
- Module discovery list lives in `src/runner/main.cpp` – keep in sync when adding modules.
- Respect GPO/settings behaviors; don’t regress elevation prompts.

Docs to consult
- `doc/devdocs/core/runner.md`