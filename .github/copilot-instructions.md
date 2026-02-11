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

## When to Ask for Clarification

- Ambiguous spec after scanning docs
- Cross-module impact unclear
- Security, elevation, or installer changes

## Component-Specific Instructions

These are auto-applied based on file location:
- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md)
- [Common Libraries](.github/instructions/common-libraries.instructions.md)

## Detailed Documentation

- [Architecture](../doc/devdocs/core/architecture.md)
- [Coding Style](../doc/devdocs/development/style.md)
