---
description: PowerToys AI contributor guidance.
applyTo: pullRequests
---

# PowerToys - Copilot guide (concise)

This is the top-level guide for AI changes. Keep edits small, follow existing patterns, and cite exact paths in PRs.

# Repo map (1-line per area)
- Core apps: `src/runner/**` (tray/loader), `src/settings-ui/**` (Settings app)
- Shared libs: `src/common/**`
- Modules: `src/modules/*` (one per utility; Command Palette in `src/modules/cmdpal/**`)
- Build tools/docs: `tools/**`, `doc/devdocs/**`

# Build and test (defaults)
- Prerequisites: Visual Studio 2022 17.4+, minimal Windows 10 1803+.
- Build discipline:
  - One terminal per operation (build -> test). Do not switch or open new ones mid-flow.
  - After making changes, `cd` to the project folder that changed (`.csproj`/`.vcxproj`).
  - Use scripts to build, synchronously block and wait in foreground for completion: `tools/build/build.ps1|.cmd` (current folder), `build-essentials.*` (once per brand new build for missing nuget packages).
  - Treat build exit code 0 as success; any non-zero exit code is a failure. Read the errors log in the build folder (such as `build.*.*.errors.log`) and surface problems.
  - Do not start tests or launch Runner until the previous step succeeded.
- Tests (fast and targeted):
  - Find the test project by product code prefix (for example FancyZones, AdvancedPaste). Look for a sibling folder or one to two levels up named like `<Product>*UnitTests` or `<Product>*UITests`.
  - Build the test project, wait for exit, then run only those tests via VS Test Explorer or `vstest.console.exe` with filters. Avoid `dotnet test` in this repo.
  - Add or adjust tests when changing behavior; if skipped, state why (for example comment-only or string rename).

# Pull requests (expectations)
- Atomic: one logical change; no drive-by refactors.
- Describe: problem, approach, risk, test evidence.
- List: touched paths if not obvious.

# When to ask for clarification
- Ambiguous spec after scanning relevant docs (see below).
- Cross-module impact (shared enum or struct) not clear.
- Security, elevation, or installer changes.

# Logging (use existing stacks)
- C++ logging lives in `src/common/logger/**` (`Logger::info`, `Logger::warn`, `Logger::error`, `Logger::debug`). Keep hot paths quiet (hooks, tight loops).
- C# logging goes through `ManagedCommon.Logger` (`LogInfo`, `LogWarning`, `LogError`, `LogDebug`, `LogTrace`). Some UIs use injected `ILogger` via `LoggerInstance.Logger`.

# Docs to consult
- `tools/build/BUILD-GUIDELINES.md`
- `doc/devdocs/core/architecture.md`
- `doc/devdocs/core/runner.md`
- `doc/devdocs/core/settings/readme.md`
- `doc/devdocs/modules/readme.md`

# Language style rules
- Always enforce repo analyzers: `src/.editorconfig` plus any `stylecop.json`.
- C# code follows StyleCop.Analyzers and Microsoft.CodeAnalysis.NetAnalyzers.
- C++ code honors `src/.clang-format` for formatting.
- Markdown files wrap at 80 characters and use ATX headers with fenced code blocks that include language tags.
- YAML files indent two spaces and add comments for complex settings while keeping keys clear.
- PowerShell scripts use Verb-Noun names and prefer single-quoted literals while documenting parameters and satisfying PSScriptAnalyzer.

# Done checklist (self review before finishing)
- Build clean? Tests updated or passed? No unintended formatting? Any new dependency? Documented skips?
