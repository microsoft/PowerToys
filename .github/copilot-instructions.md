# PowerToys – AI contributor guide (repo-specific)

Use this as a quick-start map for working effectively in this codebase. Keep edits small, follow existing patterns, and cite paths clearly in PRs.

## Big picture
- Architecture: a tray-managed Runner (`src/runner/PowerToys.exe`) loads module DLLs from `src/modules/**`. Settings UI is a separate app (`src/settings-ui`) communicating with the Runner over Windows Named Pipes using JSON messages.
- Module types: simple in-DLL utilities, external app launchers (e.g., C# WPF/WinUI UIs), Explorer context handlers (shell extensions), and registry-based handlers. All implement the shared module interface and are orchestrated by the Runner.
- Shared libs: `src/common/**` (logging, IPC, JSON, DPI helpers, etc.) are used across Runner and modules.

## Repo layout you’ll reference
- Core: `src/runner/**` (tray, keyboard hook, module loading, settings bridge), `src/common/**` (helpers), `src/settings-ui/**` (Settings app).
- Modules: `src/modules/*` (one folder per utility). The Command Palette lives under `src/modules/cmdpal/**` and ships its own extension SDK/toolkit.
- Installer: `installer/**` (EXE bootstrapper + MSI). Tools required by installer are under `tools/**`.
- Dev docs: `doc/devdocs/**` (start with `core/architecture.md`, `core/runner.md`, `core/settings/readme.md`, and `modules/readme.md`).

## Agent defaults for terminal work
- Foreground by default: run commands synchronously and wait for completion.
- One terminal per operation (restore → build → test); don’t switch/open new terminals mid-flow.
- Don’t chain unrelated steps with `&&`. Execute each step separately and only proceed on success.
- After each step, verify status (MSBuild summary and `%ERRORLEVEL%`) and provide a brief PASS/FAIL summary.
- Tests: build the specific test project first, then run `vstest.console.exe` with filters; avoid `dotnet test` in this repo.

## Build and run (local)
- Prereqs: VS 2022 17.4+, Win10 1803+; init submodules once: `git submodule update --init --recursive`.

- Quick build scripts (preferred) — see `tools/build/BUILD-GUIDELINES.md`:
  - `tools/build/build-essentials.ps1` or `tools/build/build-essentials.cmd`: run once per clone to restore NuGet and build essential native artifacts (Runner, Settings) required for local dev.
  - `tools/build/build.ps1` or `tools/build/build.cmd`: run from any folder to build projects in the current directory; auto-detects platform (x64/ARM64). For restore-only, use `-RestoreOnly`. Additional MSBuild args are forwarded.
  - `tools/build/build-installer.ps1`: builds the full installer pipeline (restore, full build, signing, MSI/bootstrapper). Use with caution—may clean and remove untracked files under `installer/`.

- Run locally:
  - Most modules can be smoked by launching the Runner: `x64\\Debug\\PowerToys.exe`.
  - Explorer shell extensions (PowerRename, ImageResizer, File Explorer add-ons) require installing via the MSI to validate, which is out of scope for you.
- Installer build is also out of scope for you please suggest back according to `tools/build/BUILD-GUIDELINES.md` for how to run it with caution.
  
Execution discipline (wait for completion):
- Use one terminal session per operation (restore → build → test). Don’t switch terminals mid-run and don’t open a new terminal mid-flow.
- Run foreground commands and wait for completion; verify the build.*.*.errors.log to see if any build error.
- Avoid chaining unrelated steps with `&&`. Build, then (only if build succeeded) run tests as separate commands.
- Don’t start the next step (tests or launching Runner) until the previous step succeeded. For tests, build the specific test project first, then run `vstest.console.exe` with a filter.
- Building from the project folder (`cd` into the .vcxproj/.csproj directory) often reduces path/env issues.
  

## Key runtime patterns to follow
- Module loading: `src/runner/main.cpp` enumerates known DLLs and calls each module’s exported `powertoy_create` to get a `PowertoyModuleIface`. Add new modules under `src/modules/<name>` and make sure they are included in Runner’s known set.
- Settings: modules expose settings via `PowerToysSettings::Settings` and persist using `PowerToysSettings::PowerToyValues` (see `tools/project_template/README.md`). Settings live under `%LOCALAPPDATA%/Microsoft/PowerToys/<module>`.
- IPC: Runner ↔ Settings UI uses two-way named pipes and JSON payloads (see `doc/devdocs/core/settings/runner-ipc.md`). Keep messages small and UI-thread marshaling explicit in Runner.
- Hotkeys: global keyboard handling is centralized in Runner to avoid perf regressions; prefer registering through Runner rather than per-module hooks.
- DPI: all UI must be DPI-aware. Use helpers in `src/common/dpi_aware.*` and `src/common/monitors.*`.

## Adding a new module (C++)
- Scaffold under `src/modules/<NewModule>/` using the template (`tools/project_template/`).
- Implement the module interface (enable/disable, hotkeys, get_config/set_config).
- Wire into Runner (known modules in `src/runner/main.cpp`) and provide a settings page via Settings API.
- Example code and patterns are documented in `tools/project_template/README.md`.

## Command Palette specifics
- Source under `src/modules/cmdpal/**`.
- Extensions implement WinRT interfaces from `Microsoft.CommandPalette.Extensions` (use the C# toolkit in `Microsoft.CommandPalette.Extensions.Toolkit`).
- Fast path: use the in-app “Create extension” command to scaffold; build the produced solution and deploy.

## Testing, logging, diagnostics
- Tests live across multiple projects; run via VS Test Explorer after building the solution. UI tests and fuzz tests are documented under `doc/devdocs/development/ui-tests.md` and `doc/devdocs/tools/fuzzingtesting.md`.
- Testing guidelines:
  - Always design code to be unit-testable (pure functions where possible, inject services, avoid hidden globals; add seams around OS/IPC calls).
  - Before adding UI automation, add or extend unit tests to cover the logic; only add new UI automation when coverage can’t be achieved via unit tests.
  - Prefer reusing existing UI automation harnesses and patterns; add new UI tests only when absolutely required for new UI.
  - When running tests, execute only the affected unit/UI tests (don’t run the entire suite) using filters. Example:
    ```cmd
    vstest.console.exe path\to\Specific.Tests.dll /Tests:FullyQualified.TestName1,FullyQualified.TestName2
    ```
  - Avoid `dotnet test` for this repo: many projects depend on Visual Studio–installed C++ build/test components that aren’t available via dotnet CLI alone.
  - If you change public behavior, update or add tests accordingly; consider fuzz tests for modules that support them.
- Logging: C++ uses spdlog via `src/common/logging/**`; prefer existing categories and don’t spam the global keyboard path.
- Bug report: `tools/BugReportTool` collects logs, system info, and installer logs; Runner can launch it (`src/runner/bug_report.cpp`).

References: `doc/devdocs/core/architecture.md`, `core/runner.md`, `core/settings/readme.md`, `modules/readme.md`, `tools/project_template/README.md`, `tools/build/BUILD-GUIDELINES.md`.
