# Repository Guidelines

## Project Structure & Module Organization
- `src/` — source code. Modules live under `src\modules\{ModuleName}` (e.g., `src\modules\fancyzones`), core apps in `src\runner`, `src\settings-ui`.
- Tests colocated with modules using `*UnitTests`, `*UITests`, `*FuzzTests` (e.g., `src\modules\imageresizer\tests`, `src\modules\fancyzones\FancyZones.UITests`).
- `tools/` — build and packaging scripts (`tools\build\*.ps1|*.cmd`).
- `installer/` — WiX/MSIX packaging assets.  `doc/` — documents and images.

## Build, Test, and Development Commands
- Quick essentials (restore + build runner/settings): `tools\build\build-essentials.cmd`
- Build current solution/folder (auto VS env): `pwsh ./tools/build/build.ps1 -Configuration Debug -Platform x64`
- Full installer (local packaging): `pwsh ./tools/build/build-installer.ps1 -Configuration Release -Platform x64 -PerUser true -InstallerSuffix wix5`
- Run .NET unit tests: `dotnet test src\modules\imageresizer\tests -c Debug`
- UI tests: open `PowerToys.sln` and run via Visual Studio Test Explorer. See `doc\devdocs\UITests.md` for WinAppDriver setup.

## Coding Style & Naming Conventions
- Follow `doc\devdocs\style.md`. Prefer Modern C++ and consistent C#/.NET style.
- Formatting: use repo `src\.clang-format` for C/C++; XAML via XamlStyler. Quick format helper: `pwsh src\codeAnalysis\format_sources.ps1`.
- Indentation: 4 spaces; names: PascalCase for types/namespaces, camelCase for locals/parameters. Tests use `{Module}.{TestType}Tests` (e.g., `FancyZones.UITests`).
- C#: adhere to StyleCop Analyzers (SAxxxx). Rules configured via `src\codeAnalysis\StyleCop.json`; suppress only in `GlobalSuppressions.cs` with justification. `dotnet build` treats warnings as errors; auto-fix where possible with `dotnet format analyzers`.

## Testing Guidelines
- Frameworks: MSTest for .NET tests; UI tests run with WinAppDriver. Some native tests exist per module.
- Place tests alongside modules; aim to cover new logic and regressions.
- Run fast unit tests locally with `dotnet test`; run UI tests in VS after building solution. See `doc\devdocs\UITests.md` for prerequisites and naming.

## Commit & Pull Request Guidelines
- Commits: imperative mood with scope. Example: `FancyZones: Fix editor crash on empty layout`.
- PRs: include description, linked issues (`Fixes #1234`), before/after screenshots for UI, test plan, and risk/rollback notes.
- Ensure builds pass (`build-essentials` at minimum) and format/lint changes before requesting review.

## Security & Configuration Tips
- Do not commit secrets/certificates. For local signing only, use `tools\build\self-sign.ps1` or `tools\build\cert-management.ps1`.
- Respect data/telemetry practices in `DATA_AND_PRIVACY.md`. Use `.vsconfig` to install required VS workloads.
