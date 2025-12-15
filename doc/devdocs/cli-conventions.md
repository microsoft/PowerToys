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

## Option Naming

### Aliases Pattern

Define option aliases as static readonly arrays following this pattern:

```csharp
private static readonly string[] AliasesSilent = ["--silent", "-s"];
private static readonly string[] AliasesWidth = ["--width", "-w"];
private static readonly string[] AliasesHelp = ["--help"];
```

When creating dedicated option types (for example, `sealed class FooOption : Option<T>`),
avoid naming a member `Aliases` (it hides `Option.Aliases`). Prefer `_aliases`.

### Naming Rules

1. **Long form**: Use `--kebab-case` (e.g., `--shrink-only`, `--keep-date-modified`)
2. **Short form**: Use single `-x` character (e.g., `-s`, `-w`, `-h`)
3. **No short form** for less common options (e.g., `--shrink-only`, `--ignore-orientation`)

## Option Definition

Create options using `Option<T>` with descriptive help text:

```csharp
var silentOption = new Option<bool>(AliasesSilent, "Run in silent mode without UI");
var widthOption = new Option<double?>(AliasesWidth, "Set width in pixels");
var unitOption = new Option<ResizeUnit?>(AliasesUnit, "Set unit (Pixel, Percent, Inch, Centimeter)");
```

## Validation

Add validators for options that require range or format checking:

```csharp
qualityOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<int?>();
    if (value.HasValue && (value.Value < 1 || value.Value > 100))
    {
        result.ErrorMessage = "JPEG quality must be between 1 and 100.";
    }
});
```

## RootCommand Setup

Create a `RootCommand` with a description and add all options:

```csharp
public static RootCommand CreateRootCommand()
{
    var rootCommand = new RootCommand("PowerToys Module Name - Brief description")
    {
        silentOption,
        widthOption,
        heightOption,
        // ... other options
        filesArgument,
    };

    return rootCommand;
}
```

## Parsing

Parse arguments and extract values:

```csharp
public static CliOptions Parse(string[] args)
{
    var options = new CliOptions();
    var rootCommand = CreateRootCommand();
    // Note: with the pinned System.CommandLine version in this repo,
    // RootCommand.Parse(args) may not be available. Use Parser instead.
    var parseResult = new Parser(rootCommand).Parse(args);

    // Extract values
    options.Silent = parseResult.GetValueForOption(silentOption);
    options.Width = parseResult.GetValueForOption(widthOption);

    return options;
}
```

### Parse/Validation Errors

If parsing or validation fails, return a non-zero exit code (and typically print
the errors plus usage):

```csharp
if (parseResult.Errors.Count > 0)
{
    foreach (var error in parseResult.Errors)
    {
        Console.Error.WriteLine(error.Message);
    }

    PrintUsage();
    return 1;
}
```

## Examples

### Awake Module

Reference implementation: `src/modules/Awake/Awake/Program.cs`

```csharp
private static readonly string[] _aliasesConfigOption = ["--use-pt-config", "-c"];
private static readonly string[] _aliasesDisplayOption = ["--display-on", "-d"];
private static readonly string[] _aliasesTimeOption = ["--time-limit", "-t"];
private static readonly string[] _aliasesPidOption = ["--pid", "-p"];
private static readonly string[] _aliasesExpireAtOption = ["--expire-at", "-e"];
```

### ImageResizer Module

Reference implementation:

- `src/modules/imageresizer/ui/Cli/Commands/ImageResizerRootCommand.cs`
- `src/modules/imageresizer/ui/Models/CliOptions.cs`
- `src/modules/imageresizer/ui/Cli/ImageResizerCliExecutor.cs`

```csharp
public sealed class DestinationOption : Option<string>
{
    private static readonly string[] _aliases = ["--destination", "-d"];

    public DestinationOption()
        : base(_aliases, "Set destination directory")
    {
    }
}
```

## Help Output

Provide a `PrintUsage()` method for custom help formatting if needed:

```csharp
public static void PrintUsage()
{
    Console.WriteLine("ModuleName - PowerToys Module CLI");
    Console.WriteLine();
    Console.WriteLine("Usage: PowerToys.ModuleName.exe [options] [arguments...]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --option, -o <value>       Description of the option");
    // ...
}
```

## Best Practices

1. **Consistency**: Follow existing patterns in the codebase (Awake, ImageResizer)
2. **Documentation**: Always provide help text for each option
3. **Validation**: Validate input values and provide clear error messages
4. **Nullable types**: Use `Option<T?>` for optional parameters
5. **Boolean flags**: Use `Option<bool>` for flags that don't require values
6. **Enum support**: System.CommandLine automatically handles enum parsing
