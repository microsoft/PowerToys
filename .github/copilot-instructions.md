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

## C# Instructions

- Always use the latest version C#, currently C# 13 features.
- Write clear and concise comments for public functions.
- Keep abstractions simple and focused
- Minimize dependencies and coupling

## General Instructions

- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions were made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.

## Naming Conventions

- Follow PascalCase for component names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., IUserService).

## Formatting

- Apply code-formatting style defined in `.editorconfig`.
- Prefer file-scoped namespace declarations and single-line using directives.
- Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Ensure that XML doc comments are created for any public APIs. When applicable, include `<example>` and `<code>` documentation in the comments.

## Testing

- Copy existing style in nearby files for test method names and capitalization.

## Performance Optimization

- Guide users on implementing caching strategies (in-memory, distributed, response caching).
- Explain asynchronous programming patterns and why they matter for API performance.
- Show how to implement compression and other performance optimizations.

## Additional Notes

- For new features or fixes, file an issue and discuss before starting work (see `CONTRIBUTING.md`).
- Some module READMEs have been moved to aka.ms links; check those for up-to-date docs.
