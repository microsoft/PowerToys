# CLI Conventions

This document describes the conventions for implementing command-line interfaces (CLI) in PowerToys modules.

## Library

Use the **System.CommandLine** library for CLI argument parsing. This is already defined in `Directory.Packages.props`:

```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
```

Add the reference to your project:

```xml
<PackageReference Include="System.CommandLine" />
```

## Option Naming and Definition

- Use `--kebab-case` for long form (e.g., `--shrink-only`).
- Use single `-x` for short form (e.g., `-s`, `-w`).
- Define aliases as static readonly arrays: `["--silent", "-s"]`.
- Create options using `Option<T>` with descriptive help text.
- Add validators for options that require range or format checking.

## RootCommand Setup

- Create a `RootCommand` with a brief description.
- Add all options and arguments to the command.

## Parsing

- Use `Parser(rootCommand).Parse(args)` to parse CLI arguments.
- Extract option values using `parseResult.GetValueForOption()`.
- Note: Use `Parser` directly; `RootCommand.Parse()` may not be available with the pinned System.CommandLine version.

### Parse/Validation Errors

- On parse/validation errors, print error messages and usage, then exit with non-zero code.

## Examples

Reference implementations:
- Awake: `src/modules/Awake/Awake/Program.cs`
- ImageResizer: `src/modules/imageresizer/ui/Cli/`

## Help Output

- Provide a `PrintUsage()` method for custom help formatting if needed.

## Best Practices

1. **Consistency**: Follow existing module patterns.
2. **Documentation**: Always provide help text for each option.
3. **Validation**: Validate input and provide clear error messages.
4. **Atomicity**: Make one logical change per PR; avoid drive-by refactors.
5. **Build/Test Discipline**: Build and test synchronously, one terminal per operation.
6. **Style**: Follow repo analyzers (`.editorconfig`, StyleCop) and formatting rules.

## Logging Requirements

- Use `ManagedCommon.Logger` for consistent logging.
- Initialize logging early in `Main()`.
- Use dual output (console + log file) for errors and warnings to ensure visibility.
- Reference: `src/modules/imageresizer/ui/Cli/CliLogger.cs`

## Error Handling

### Exit Codes

Use `0` for success and a non-zero code for failure. A minimal CLI can use:

- `0`: Success
- `1`: General error (parsing, validation, runtime)
- `2`: Invalid arguments (optional)

Modules **MAY** define a richer, module-specific exit-code scheme when scripts benefit from
distinguishing failure kinds (e.g. not-found vs. out-of-range vs. hardware failure). When you do:

- Keep the code→meaning mapping in one place (a single source of truth) so an error's code and its
  exit code cannot drift.
- **Document it in the module's own docs** — do not assume the minimal `1`/`2` meanings above carry
  over. In a richer scheme `2` may mean something else (e.g. "out of range"), so a consumer must read
  the module's table, not this baseline.

For a worked example see the PowerDisplay CLI ([`modules/powerdisplay/cli.md`](modules/powerdisplay/cli.md)),
which maps ten distinct error codes to exit codes `1`–`10`.

### Exception Handling

- Always wrap `Main()` in try-catch for unhandled exceptions.
- Log exceptions before exiting with non-zero code.
- Display user-friendly error messages to stderr.
- Preserve detailed stack traces in log files only.

## Testing Requirements

- Include tests for argument parsing, validation, and edge cases.
- Place CLI tests in module-specific test projects (e.g., `src/modules/[module]/tests/*CliTests.cs`).

## Signing and Deployment

- CLI executables are signed automatically in CI/CD.
- **New CLI tools**: Add your executable and dll to `.pipelines/ESRPSigning_core.json` in the signing list.
- CLI executables are deployed alongside their parent module (e.g., `C:\Program Files\PowerToys\modules\[ModuleName]\`).
- Use self-contained deployment (import `Common.SelfContained.props`).
