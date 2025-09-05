---
applyTo: "**/*.cpp,**/*.c,**/*.h,**/*.hpp,**/*.rc"
---
# Runner – tray / host process guidance

Scope
- Module bootstrap, hotkey management, settings bridge, update/elevation handling.

Guidelines
- If IPC/JSON contracts change, mirror updates in `src/settings-ui/**`.
- Keep module discovery in `src/runner/main.cpp` in sync when adding/removing modules.
- Keep startup lean: avoid blocking/network calls in early init path.
- Preserve GPO & elevation behaviors; confirm no regression in policy handling.
- Ask before modifying update workflow or elevation logic.

Acceptance
- Stable startup, consistent contracts, no unnecessary logging noise.