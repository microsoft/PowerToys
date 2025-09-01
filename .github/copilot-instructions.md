# PowerToys – Copilot guide (concise)

This is the top-level guide for AI changes. Keep edits small, follow existing patterns, and cite exact paths in PRs.

Before you start
- Open copilot-instructions.md in the target folder (or the nearest parent). Follow that folder’s rules first. If none exists, use the defaults below.

Repo map (1‑line per area)
- Core apps: `src/runner/**` (tray/loader), `src/settings-ui/**` (Settings app)
- Shared libs: `src/common/**`
- Modules: `src/modules/*` (one per utility; Command Palette in `src/modules/cmdpal/**`)
- Build tools/docs: `tools/**`, `doc/devdocs/**`

Build and test (defaults)
- Prereqs: Visual Studio 2022 17.4+, Windows 10 1803+. Initialize submodules once.
- Use scripts: `tools/build/build.ps1|.cmd` (current folder), `build-essentials.*` (once per clone), `build-installer.ps1` (full pipeline, slow).
- Execution discipline:
  - One terminal per operation (restore → build → test). Don’t switch/open new ones mid-flow.
  - After making changes, cd to the project that changed (`.csproj`/`.vcxproj`) and build there first; only then build from repo root if needed.
  - Run commands in the foreground and wait. When build finishes, have Copilot read the errors log in the build folder (e.g., `build.*.*.errors.log`) and surface problems.
  - Don’t start tests or launch Runner until the previous step succeeded.
- Tests (fast + targeted):
  - Find the test project by product code prefix (e.g., FancyZones, AdvancedPaste). Look for a sibling folder or 1–2 levels up named like `<Product>*UnitTests` or `<Product>*UITests`.
  - Build the test project, then run only those tests via VS Test Explorer or `vstest.console.exe` with filters. Avoid `dotnet test` in this repo.

Logging (use existing stacks)
- C++: `src/common/logger/**` (`Logger::info|warn|error|debug`). Keep hot paths quiet (hooks, tight loops).
- C#: `ManagedCommon.Logger` (`LogInfo|LogWarning|LogError|LogDebug|LogTrace`). Some UIs use injected `ILogger` via `LoggerInstance.Logger`.

Where folder rules live (do this first)
- Each area should include a short `copilot-instructions.md` at its root with: how to build just this area, which tests to run, logging expectations, perf/UX gotchas, and acceptance checks.
- If the folder has one, follow it over this top-level guide. If it’s a new project, create a minimal one based on the template in `tools/project_template/`.

Docs to consult
- `tools/build/BUILD-GUIDELINES.md`
- `doc/devdocs/core/architecture.md`, `doc/devdocs/core/runner.md`, `doc/devdocs/core/settings/readme.md`, `doc/devdocs/modules/readme.md`