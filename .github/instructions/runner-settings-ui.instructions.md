---
description: 'Guidelines for Runner and Settings UI components that communicate via named pipes and manage module lifecycle'
applyTo: 'src/runner/**,src/settings-ui/**'
---

# Runner & Settings UI – Core Components Guidance

Guidelines for modifying the Runner (tray/module loader) and Settings UI (configuration app). These components communicate via Windows Named Pipes using JSON messages.

## Runner (`src/runner/`)

### Scope

- Module bootstrap, hotkey management, settings bridge, update/elevation handling

### Guidelines

- If IPC/JSON contracts change, mirror updates in `src/settings-ui/**`
- Keep module discovery in `src/runner/main.cpp` in sync when adding/removing modules
- Keep startup lean: avoid blocking/network calls in early init path
- Preserve GPO & elevation behaviors; confirm no regression in policy handling
- Ask before modifying update workflow or elevation logic

### Acceptance Criteria

- Stable startup, consistent contracts, no unnecessary logging noise

## Settings UI (`src/settings-ui/`)

### Scope

- WinUI/WPF UI, communicates with Runner over named pipes; manages persisted settings schema

### Guidelines

- Don't break settings schema silently; add migration when shape changes
- If IPC/JSON contracts change, align with `src/runner/**` implementation
- Keep UI responsive: marshal to UI thread for UI-bound operations
- Reuse existing styles/resources; avoid duplicate theme keys
- Add/adjust migration or serialization tests when changing persisted settings

### Acceptance Criteria

- Schema integrity preserved, responsive UI, consistent contracts, no style duplication

## Shared Concerns

### IPC Contract Changes

When modifying the JSON message format between Runner and Settings UI:

1. Update both `src/runner/` and `src/settings-ui/` in the same PR
2. Preserve backward compatibility where possible
3. Add migration logic for settings schema changes
4. Test both directions of communication

### Code Style

- **C++ (Runner)**: Follow `.clang-format` in `src/`
- **C# (Settings UI)**: Follow `src/.editorconfig`, use StyleCop.Analyzers
- **XAML**: Use XamlStyler or run `.\.pipelines\applyXamlStyling.ps1 -Main`

## Validation

- Build Runner: `tools\build\build.cmd` from `src/runner/`
- Build Settings UI: `tools\build\build.cmd` from `src/settings-ui/`
- Test IPC: Launch both Runner and Settings UI, verify communication works
- Schema changes: Run serialization tests if settings shape changed
