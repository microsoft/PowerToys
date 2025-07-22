# Copilot Instructions for PowerToys

## Architecture Overview
- **PowerToys** is a suite of Windows utilities, each implemented as a separate module (DLL) in `src/modules`. The main `runner` (in `src/runner`) loads and manages these modules.
- The **Settings UI** is a standalone executable in `src/settings-ui`, using WebView for an HTML-based interface.
- Shared code lives in `src/common`.
- Many modules have their own sub-folders and may include C++, C#, and interop code.

## Build & Test
- Use the root `PowerToys.sln` for building in Visual Studio. All major modules and utilities are included.
- For .NET projects, StyleCop and Roslyn analyzers are enforced (see `Directory.Build.props` and `src/.editorconfig`).
- To run tests: use Visual Studio Test Explorer or `msbuild /t:Test` (not supported for ARM64, see `Directory.Build.props`).
- Some modules have additional CMake-based native dependencies in `deps/`.

## Coding Conventions
- C# code style is enforced via `src/.editorconfig` and `src/codeAnalysis/StyleCop.json`.
  - No required XML docs; file headers are required.
  - Interface names must start with `I`.
  - PascalCase for types and members.
  - Underscore prefix for private fields is allowed.
  - `this.` is avoided except when necessary.
- Project-level suppressions are in `src/codeAnalysis/GlobalSuppressions.cs`.
- Treat warnings as errors (`Directory.Build.props`).

## Extensibility & Patterns
- **Interop**: Many modules use both C++ and C#. Shared logic is in `src/common`.
- **Settings**: Each module manages its own settings, surfaced in the Settings UI.

## Key Files & Directories
- `PowerToys.sln`: Main solution file.
- `src/modules/`: All PowerToys modules.
- `src/runner/`: Main host executable.
- `src/settings-ui/`: Settings window.
- `src/common/`: Shared code.
- `Directory.Build.props`/`targets`: Build configuration and analyzer settings.
- `src/.editorconfig`, `src/codeAnalysis/StyleCop.json`: Coding style.
- `src/codeAnalysis/GlobalSuppressions.cs`: Analyzer suppressions.

## C# Specific Requirements
- Write clear, self-documenting code
- Keep abstractions simple and focused
- Minimize dependencies and coupling
- Use modern C# features appropriately

## Additional Notes
- For new features or fixes, file an issue and discuss before starting work (see `CONTRIBUTING.md`).
- Some module READMEs have been moved to aka.ms links; check those for up-to-date docs.
