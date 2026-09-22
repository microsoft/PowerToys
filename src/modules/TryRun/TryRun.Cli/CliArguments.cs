// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Cli;

public sealed record CliArguments(CliCommand Command)
{
    public const int MaximumCharacters = 65536;

    public string? PolicyId { get; init; }

    public string? Revision { get; init; }

    public string? File { get; init; }

    public string[] Inputs { get; init; } = [];

    public string[] Arguments { get; init; } = [];

    public string? Output { get; init; }

    public static CliArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length > 256 || args.Any(value => value is null || value.Length > 8192 || value.Contains('\0')) || args.Sum(value => (long)value.Length) > MaximumCharacters)
        {
            throw new ArgumentException("The command is too large or contains invalid characters.");
        }

        if (args.Length == 0 || (args.Length == 1 && args[0] is "--help" or "-h"))
        {
            return new(CliCommand.Help);
        }

        if (args.Length == 2 && args[0] == "policies" && args[1] == "list")
        {
            return new(CliCommand.ListPolicies);
        }

        if (args.Length == 3 && args[0] == "policies" && args[1] == "show")
        {
            return new(CliCommand.ShowPolicy) { PolicyId = ValidateId(args[2]) };
        }

        if (args[0] != "run" || args.Length % 2 != 1)
        {
            throw new ArgumentException("Use policies list, policies show <id>, or run with named option/value pairs. See --help.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inputs = new List<string>();
        var arguments = new List<string>();
        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            var value = args[index + 1];
            if (option == "--input")
            {
                inputs.Add(LocalPath(value));
            }
            else if (option == "--arg")
            {
                arguments.Add(value);
            }
            else if (option is "--policy" or "--revision" or "--file" or "--output")
            {
                if (!values.TryAdd(option, value))
                {
                    throw new ArgumentException($"Specify {option} only once.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown option: {option}. Inline policy overrides are not supported.");
            }
        }

        if (!values.TryGetValue("--policy", out var policyId) || !values.TryGetValue("--revision", out var revision) || !values.TryGetValue("--file", out var file))
        {
            throw new ArgumentException("run requires --policy <id>, --revision <sha256>, and --file <absolute path>. Review the revision with policies show first.");
        }

        ValidateId(policyId);
        if (revision.Length != 64 || revision.Any(character => !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("The revision must be the lowercase 64-character SHA-256 returned by policies show.");
        }

        if (inputs.Count > 64 || arguments.Count > 64 || arguments.Any(argument => argument.Length > 4096) || arguments.Sum(argument => argument.Length) > 8192)
        {
            throw new ArgumentException("Use at most 64 inputs and 64 arguments; arguments may contain at most 8,192 characters in total.");
        }

        file = LocalPath(file);
        _ = WindowsKind(file);
        return new(CliCommand.Run)
        {
            PolicyId = policyId,
            Revision = revision,
            File = file,
            Inputs = inputs.ToArray(),
            Arguments = arguments.ToArray(),
            Output = values.TryGetValue("--output", out var output) ? LocalPath(output) : null,
        };
    }

    public static WorkloadKind WindowsKind(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".exe" => WorkloadKind.WindowsApplication,
        ".ps1" => WorkloadKind.WindowsPowerShell,
        ".cmd" or ".bat" => WorkloadKind.WindowsBatch,
        _ => throw new ArgumentException("This saved-policy entry point supports Windows .exe, .ps1, .cmd, and .bat files. Use the Try Run application for Linux workloads."),
    };

    private static string ValidateId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out var parsed) || id != parsed.ToString("N"))
        {
            throw new ArgumentException("Choose a policy ID returned by policies list.");
        }

        return id;
    }

    private static string LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path[3..].Contains(':') || path.Any(char.IsControl) || path.Contains('"'))
        {
            throw new ArgumentException("Use an absolute local drive path without command-line arguments or alternate data streams.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
