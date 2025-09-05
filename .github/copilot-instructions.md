# PowerToys – Copilot guide (concise)

This is the top-level guide for AI changes. Keep edits small, follow existing patterns, and cite exact paths in PRs.

Repo map (1‑line per area)
- Core apps: `src/runner/**` (tray/loader), `src/settings-ui/**` (Settings app)
- Shared libs: `src/common/**`
- Modules: `src/modules/*` (one per utility; Command Palette in `src/modules/cmdpal/**`)
- Build tools/docs: `tools/**`, `doc/devdocs/**`

Build and test (defaults)
- Prerequisites: Visual Studio 2022 17.4+, minimal Windows 10 1803+.
- Build discipline:
  - One terminal per operation (build → test). Don’t switch/open new ones mid-flow.
  - After making changes, `cd` to the project folder that changed (`.csproj`/`.vcxproj`).
  - Use script(s) to build, synchronously block and wait in foreground for it to finish: `tools/build/build.ps1|.cmd` (current folder), `build-essentials.*` (once per brand new build for missing nuget packages)
  - Treat build **exit code 0** as success; any non-zero exit code is a failure, have Copilot read the errors log in the build folder (e.g., `build.*.*.errors.log`) and surface problems.
  - Don’t start tests or launch Runner until the previous step succeeded.
- Tests (fast + targeted):
  - Find the test project by product code prefix (e.g., FancyZones, AdvancedPaste). Look for a sibling folder or 1–2 levels up named like `<Product>*UnitTests` or `<Product>*UITests`.
  - Build the test project, wait for **exit**, then run only those tests via VS Test Explorer or `vstest.console.exe` with filters. Avoid `dotnet test` in this repo.
  - Add/adjust tests when changing behavior; if skipped, state why (e.g., comment-only, string rename).

Pull requests (expectations)
- Atomic: one logical change; no drive‑by refactors.
- Describe: problem / approach / risk / test evidence.
- List: touched paths if not obvious.

When to ask for clarification
- Ambiguous spec after scanning relevant docs (see below).
- Cross-module impact (shared enum/struct) not clear.
- Security / elevation / installer changes.

Do / Don’t quick list
- Do: verify paths & APIs before editing; keep diffs minimal; add tests for logic.
- Don’t: reformat unrelated code; introduce global singletons; add logging in tight loops.

Logging (use existing stacks)
- C++: `src/common/logger/**` (`Logger::info|warn|error|debug`). Keep hot paths quiet (hooks, tight loops).
- C#: `ManagedCommon.Logger` (`LogInfo|LogWarning|LogError|LogDebug|LogTrace`). Some UIs use injected `ILogger` via `LoggerInstance.Logger`.

Docs to consult
- `tools/build/BUILD-GUIDELINES.md`
- `doc/devdocs/core/architecture.md`, `doc/devdocs/core/runner.md`, `doc/devdocs/core/settings/readme.md`, `doc/devdocs/modules/readme.md`

Done checklist (self review before finishing)
- Build clean? Tests updated/passed? No unintended formatting? Any new dependency? Documented skips?