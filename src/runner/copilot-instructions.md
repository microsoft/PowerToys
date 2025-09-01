# Runner – Copilot guide (short)

Scope
- Tray app that loads modules, manages hotkeys, settings bridge, updates.

Specific Guideline
- If you changed IPC/JSON, verify the code at `/src/settings-ui/**` for consistency.
- Module discovery list lives in `src/runner/main.cpp` – keep in sync when adding modules.
- Respect GPO/settings behaviors; don’t regress elevation prompts.

Docs to consult
- `doc/devdocs/core/runner.md`