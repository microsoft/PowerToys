---
description: 'PowerToys AI contributor guidance'
---

# PowerToys – Copilot Instructions

Concise guidance for AI contributions. For complete details, see [AGENTS.md](../AGENTS.md).

## Key Rules

- Atomic PRs: one logical change, no drive-by refactors
- Add tests when changing behavior
- Keep hot paths quiet (no logging in hooks/tight loops)

## Style Enforcement

- C#: `src/.editorconfig`, StyleCop.Analyzers
- C++: `src/.clang-format`
- XAML: XamlStyler

## Build & Test

- First build / NuGet restore: `tools\build\build-essentials.cmd`
- Build only the affected folder: `tools\build\build.cmd -Path <folder>`
- Use VS Test Explorer or `vstest.console.exe`; avoid `dotnet test` for repo tests
- If you change Runner or Settings UI IPC/contracts, follow `.github/instructions/runner-settings-ui.instructions.md`
- If you change shared code in `src/common`, follow `.github/instructions/common-libraries.instructions.md`

## When to Ask for Clarification

- Ambiguous spec after scanning docs
- Cross-module impact unclear
- Security, elevation, or installer changes

## Component-Specific Instructions

These are auto-applied based on file location:
- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md)
- [Common Libraries](.github/instructions/common-libraries.instructions.md)

## Shortcut Guide V2 Manifests

When creating or editing Shortcut Guide keyboard shortcut manifest files, follow the schema and naming conventions in the spec:

- [WinGet Manifest Keyboard Shortcuts schema](<../doc/specs/WinGet Manifest Keyboard Shortcuts schema.md>) – manifest file format, field definitions, file naming, and the `+` prefix convention for apps without a WinGet package

## Detailed Documentation

- [Architecture](../doc/devdocs/core/architecture.md)
- [Coding Style](../doc/devdocs/development/style.md)