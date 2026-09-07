# CLI Conventions

This document describes the conventions for implementing command-line interfaces (CLI) in PowerToys modules.

## PATH-Visible Command Naming and Location

- Name module CLI command shims `PowerToys.<ModuleName>.CLI.exe` (for example, `PowerToys.ImageResizer.CLI.exe`).
- Install these shims in the `bin` subfolder of the PowerToys installation directory, which the installer adds to `PATH`.

Every command is the same `PowerToys.CliShim.exe` payload (`tools/CliShim/`) installed under a different name. The shim resolves which CLI to launch from its own file name, forwards the raw argument tail unchanged, shares the caller's console, and returns the CLI's exit code. The CLI runs in a job object owned by the shim, so killing the shim kills the CLI with it; processes the CLI itself starts (the Settings window, for example) break away and survive.

On a per-machine install the `bin` folder is created with a protected DACL (`MachinePathFolderSddl` in `installer/PowerToysSetupVNext/Common.wxi`) so that a custom installation root cannot leave a machine-`PATH` folder writable by standard users. Author that `<CreateFolder>` on the same component as the folder's `<Environment>` `PATH` entry, so the two cannot drift apart.

### Adding a new shim

1. Add a `<CliShim>` item to `tools/CliShim/CliShimManifest.props` with the command name and the target's path relative to `bin`. Write that path with `/` separators, and against the *installed* layout (see [Signing and Deployment](#signing-and-deployment)) - which is where the CLI ends up, not where it is built from.
2. Add the matching `<Component>` and `<ComponentRef>` to `installer/PowerToysSetupVNext/CliShims.wxs`, using the command name as the `File/@Name`.

`CliShim.vcxproj` fails the build if the command names in those two drift apart, `build-installer.ps1` fails the build if a `RelativeTarget` does not resolve to a real executable, and `CliShim.UnitTests` generates its expectations from the same manifest, so there is no third list to update.

### Shim exit codes

The shim returns the target CLI's exit code unchanged. It substitutes one of its own codes only when the CLI never ran, using values outside the range the CLIs use themselves:

| Code | Meaning |
| --- | --- |
| `9009` | No CLI is mapped to the invoked command name (matches `cmd.exe`'s "command not found"). |
| `9010` | The mapped target executable is missing from the installation. |
| `9011` | The shim could not start the target, including when it cannot resolve its own path. |

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

- `0`: Success
- `1`: General error (parsing, validation, runtime)
- `2`: Invalid arguments (optional)

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
- CLI executables are deployed either to the installation root (e.g., `C:\Program Files\PowerToys\FancyZonesCLI.exe`) or, for WinUI 3 modules, next to their module in `WinUI3Apps\` (e.g., `C:\Program Files\PowerToys\WinUI3Apps\PowerToys.ImageResizerCLI.exe`). PATH-visible shims are deployed to `C:\Program Files\PowerToys\bin\`, and a shim's `RelativeTarget` is resolved from that `bin` folder against the *installed* layout - not against the source tree.
- Use self-contained deployment (import `Common.SelfContained.props`).
