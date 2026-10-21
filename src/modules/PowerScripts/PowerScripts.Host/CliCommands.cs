// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PowerScripts.Core;
using PowerScripts.Core.Cli;

namespace PowerScripts.Host;

/// <summary>
/// The process/JSON contract for the Host's embedded native CLI catalogue. This route never loads a
/// script, accepts an executable path, or records script trust. Native launches still go through the
/// shared non-elevated, MXC-aware process runner in Core.
/// </summary>
internal static class CliCommands
{
    private const int DefaultTimeoutMs = CliExecutor.DefaultTimeoutMs;
    private const int MaximumTimeoutMs = CliExecutor.MaximumTimeoutMs;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>All CLI responses, including usage failures, are structured JSON.</summary>
    public static int Run(IReadOnlyList<string> arguments)
    {
        bool listing = arguments.Count > 0 && string.Equals(arguments[0], "list", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (listing)
            {
                return List(arguments);
            }

            if (arguments.Count > 0 && string.Equals(arguments[0], "run", StringComparison.OrdinalIgnoreCase))
            {
                return Execute(arguments);
            }

            return FailRun("invalidParameters", 1, "Expected 'cli list' or 'cli run <id>'.");
        }
        catch (Exception ex)
        {
            return listing
                ? FailCatalog($"Unable to read the built-in CLI catalogue: {ex.Message}", 2)
                : FailRun("error", 2, $"Unable to run the built-in CLI command: {ex.Message}");
        }
    }

    private static int List(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments.Skip(1))
        {
            if (!string.Equals(argument, "--json", StringComparison.Ordinal))
            {
                return FailCatalog($"Unknown CLI catalogue option '{argument}'. The built-in catalogue cannot be overridden.", 1);
            }
        }

        var catalog = BuiltinCliCatalog.Load();
        var executor = new CliExecutor(ResolveInstallationRoot());
        bool enabled = ModuleState.IsPowerScriptsEnabled();
        var commands = catalog.Commands.Select(command =>
        {
            var availability = executor.CheckAvailability(command);
            return new
            {
                command.Id,
                command.Name,
                command.Group,
                command.Description,
                isAvailable = enabled && availability.IsAvailable,
                unavailableReason = !enabled
                    ? "PowerScripts is disabled. Enable PowerScripts to use CLI bindings."
                    : availability.IsAvailable ? null : availability.UnavailableReason,
                parameters = command.Parameters.Select(parameter => new
                {
                    parameter.Name,
                    parameter.Type,
                    parameter.IsRequired,
                    parameter.Label,
                    parameter.Description,
                    parameter.Default,
                    parameter.Options,
                    parameter.Min,
                    parameter.Max,
                }),
            };
        });

        WriteJson(new { succeeded = true, error = (string?)null, commands });
        return 0;
    }

    private static int Execute(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || !IsStableId(arguments[1]))
        {
            return FailRun("invalidParameters", 1, "Expected a built-in CLI command ID containing only letters, digits, '.', '_' or '-'.");
        }

        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        int timeoutMs = DefaultTimeoutMs;
        bool hasTimeout = false;
        for (var index = 2; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (string.Equals(option, "--json", StringComparison.Ordinal) ||
                string.Equals(option, "--no-consent", StringComparison.Ordinal))
            {
                // Embedded commands never prompt for script trust. Accept the explicit automatic-
                // invocation flag without allowing it to change the effective execution policy.
                continue;
            }

            if (!string.Equals(option, "--set", StringComparison.Ordinal) &&
                !string.Equals(option, "--set-base64", StringComparison.Ordinal) &&
                !string.Equals(option, "--timeout-ms", StringComparison.Ordinal))
            {
                return FailRun("invalidParameters", 1, $"Unknown CLI option '{option}'. Executable paths and catalogue roots cannot be overridden.");
            }

            if (++index >= arguments.Count)
            {
                return FailRun("invalidParameters", 1, $"Missing value for '{option}'.");
            }

            string value = arguments[index];
            if (string.Equals(option, "--timeout-ms", StringComparison.Ordinal))
            {
                if (hasTimeout || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutMs) ||
                    timeoutMs < 1 || timeoutMs > MaximumTimeoutMs)
                {
                    return FailRun("invalidParameters", 1, $"--timeout-ms must be specified once and be between 1 and {MaximumTimeoutMs}.");
                }

                hasTimeout = true;
                continue;
            }

            if (!TryReadParameter(option, value, out var name, out var parameterValue, out var error))
            {
                return FailRun("invalidParameters", 1, error);
            }

            if (!parameters.TryAdd(name, parameterValue))
            {
                return FailRun("invalidParameters", 1, $"Parameter '{name}' was supplied more than once.");
            }
        }

        var command = BuiltinCliCatalog.Load().Get(arguments[1]);
        if (command is null)
        {
            return FailRun("unknownCommand", 1, $"The built-in CLI command '{arguments[1]}' is not defined.");
        }

        if (!ModuleState.IsPowerScriptsEnabled())
        {
            return FailRun("disabled", 3, "PowerScripts is disabled in PowerToys settings. Enable PowerScripts to use this binding.");
        }

        if (!command.TryResolveParameters(parameters, out var resolved, out var parameterError))
        {
            return FailRun("invalidParameters", 1, parameterError);
        }

        var result = new CliExecutor(ResolveInstallationRoot()).Execute(command, resolved, timeoutMs);
        string? executionError = result.Succeeded
            ? null
            : string.IsNullOrWhiteSpace(result.StdErr)
                ? $"The CLI command failed with status '{result.Status}' and exit code {result.ExitCode}."
                : result.StdErr;
        WriteRunResult(result.Succeeded, result.Status, result.ExitCode, result.StdOut, result.StdErr, executionError);
        return result.Succeeded ? 0 : result.ExitCode == 0 ? 2 : result.ExitCode;
    }

    private static bool TryReadParameter(string option, string supplied, out string name, out string value, out string error)
    {
        name = string.Empty;
        value = string.Empty;
        error = string.Empty;
        bool encoded = string.Equals(option, "--set-base64", StringComparison.Ordinal);
        string decoded = supplied;
        if (encoded)
        {
            try
            {
                decoded = StrictUtf8.GetString(Convert.FromBase64String(supplied));
            }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
            {
                error = "--set-base64 must contain a valid Base64-encoded UTF-8 name, NUL separator and value.";
                return false;
            }
        }

        int separator = decoded.IndexOf(encoded ? '\0' : '=');
        if (separator <= 0)
        {
            error = encoded ? "--set-base64 is missing a parameter name or NUL separator." : "--set expects name=value.";
            return false;
        }

        name = decoded[..separator];
        value = decoded[(separator + 1)..];
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\0') || value.Contains('\0'))
        {
            error = "CLI parameter names must be non-empty, and parameter names and values cannot contain NUL characters.";
            return false;
        }

        return true;
    }

    private static bool IsStableId(string id) =>
        !string.IsNullOrEmpty(id) && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    /// <summary>
    /// Installed Hosts live in the installation's PowerScripts subdirectory. A Host running from
    /// this project's bin directory may use only its own repository's matching platform/configuration
    /// output; callers cannot choose the executable root through arguments or environment variables.
    /// </summary>
    private static string ResolveInstallationRoot()
    {
        var hostDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        if (hostDirectory.Name.Equals("PowerScripts", StringComparison.OrdinalIgnoreCase) && hostDirectory.Parent is { } parent)
        {
            return parent.FullName;
        }

        for (var directory = hostDirectory; directory is not null; directory = directory.Parent)
        {
            var projectDirectory = Path.Combine(directory.FullName, "src", "modules", "PowerScripts", "PowerScripts.Host");
            if (!File.Exists(Path.Combine(directory.FullName, "PowerToys.slnx")) ||
                !File.Exists(Path.Combine(projectDirectory, "PowerScripts.Host.csproj")))
            {
                continue;
            }

            string relative = Path.GetRelativePath(Path.Combine(projectDirectory, "bin"), hostDirectory.FullName);
            var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 ||
                (!segments[0].Equals("Debug", StringComparison.OrdinalIgnoreCase) && !segments[0].Equals("Release", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            string? platform = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "ARM64",
                _ => null,
            };
            if (platform is not null)
            {
                string configuration = segments[0].Equals("Debug", StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";
                return Path.Combine(directory.FullName, platform, configuration);
            }

            break;
        }

        return hostDirectory.FullName;
    }

    private static int FailCatalog(string error, int exitCode)
    {
        WriteJson(new { succeeded = false, error, commands = Array.Empty<object>() });
        return exitCode;
    }

    private static int FailRun(string status, int exitCode, string error)
    {
        WriteRunResult(false, status, exitCode, string.Empty, error, error);
        return exitCode;
    }

    private static void WriteRunResult(bool succeeded, string status, int exitCode, string stdOut, string stdErr, string? error) =>
        WriteJson(new { succeeded, status, exitCode, stdOut, stdErr, error });

    private static void WriteJson(object value) => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
}
