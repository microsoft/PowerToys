// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Cli;

public sealed class CliAvailability
{
    public bool IsAvailable { get; init; }

    public string UnavailableReason { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }
}

public sealed class CliExecutionResult
{
    public string Status { get; init; } = "failed";

    public int ExitCode { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public bool Succeeded => Status == "succeeded" && ExitCode == 0;
}

/// <summary>Executes a built-in definition through the existing centralized process/security policy.</summary>
public sealed class CliExecutor
{
    public const int DefaultTimeoutMs = 60000;
    public const int MaximumTimeoutMs = 120000;

    private readonly Func<ProcessStartInfo, ProcessExecutionContext, int, ProcessRunResult> _run;

    public CliExecutor(string? installationRoot = null)
        : this(installationRoot ?? DefaultInstallationRoot(), (start, context, timeout) =>
            ProcessRunner.Run(start, context, timeoutMs: timeout))
    {
    }

    internal CliExecutor(
        string installationRoot,
        Func<ProcessStartInfo, ProcessExecutionContext, int, ProcessRunResult> run)
    {
        ArgumentNullException.ThrowIfNull(installationRoot);
        ArgumentNullException.ThrowIfNull(run);
        InstallationRoot = installationRoot;
        _run = run;
    }

    public string InstallationRoot { get; }

    /// <summary>Checks the installed executable path without probing or starting the module.</summary>
    public CliAvailability CheckAvailability(BuiltinCliDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var available = CliExecutableResolver.TryResolve(InstallationRoot, definition.Executable, out var path, out var reason);
        return new CliAvailability { IsAvailable = available, ExecutablePath = path, UnavailableReason = reason };
    }

    public CliExecutionResult Execute(
        BuiltinCliDefinition definition,
        IReadOnlyDictionary<string, string?> parameters,
        int timeoutMs = DefaultTimeoutMs)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(parameters);
        if (timeoutMs <= 0 || timeoutMs > MaximumTimeoutMs)
        {
            return Failure("invalidParameters", 1, $"Timeout must be between 1 and {MaximumTimeoutMs} milliseconds.");
        }

        if (!definition.TryResolveParameters(parameters, out var resolved, out var error))
        {
            return Failure("invalidParameters", 1, error);
        }

        var availability = CheckAvailability(definition);
        if (!availability.IsAvailable)
        {
            return Failure("unavailable", 2, availability.UnavailableReason);
        }

        var start = new ProcessStartInfo
        {
            FileName = availability.ExecutablePath!,
            WorkingDirectory = Path.GetDirectoryName(availability.ExecutablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in definition.FixedArguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var binding in definition.Bindings)
        {
            if (!resolved.TryGetValue(binding.Parameter, out var value) || value is null)
            {
                continue;
            }

            switch (binding.Kind)
            {
                case CliArgumentKind.Positional:
                    start.ArgumentList.Add(value);
                    break;
                case CliArgumentKind.Option:
                    start.ArgumentList.Add(binding.Name!);
                    start.ArgumentList.Add(value);
                    break;
                case CliArgumentKind.Switch:
                    if (string.Equals(value, "true", StringComparison.Ordinal))
                    {
                        start.ArgumentList.Add(binding.Name!);
                    }

                    break;
                default:
                    return Failure("invalidParameters", 1, "The command has an unsupported argument binding.");
            }
        }

        var context = new ProcessExecutionContext
        {
            ScriptId = "builtin-cli:" + definition.Id,
            ScriptDirectory = InstallationRoot,
            ScriptsRoot = InstallationRoot,
            InputPaths = definition.Parameters
                .Where(parameter => parameter.IsFile && resolved.TryGetValue(parameter.Name, out var path) && !string.IsNullOrEmpty(path))
                .Select(parameter => resolved[parameter.Name]!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            RecommendedPolicies = MxcPolicies.All,
        };

        try
        {
            // No script trust records or MXC preferences are created/changed by native CLI execution.
            var result = _run(start, context, timeoutMs);
            return new CliExecutionResult
            {
                Status = result.ExitCode switch
                {
                    0 => "succeeded",
                    124 => "timeout",
                    125 or 126 => "restricted",
                    _ => "failed",
                },
                ExitCode = result.ExitCode,
                StdOut = result.StdOut,
                StdErr = result.StdErr,
            };
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Failure("failed", 2, $"The command could not be started: {ex.Message}");
        }
    }

    private static string DefaultInstallationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        return string.Equals(directory.Name, "PowerScripts", StringComparison.OrdinalIgnoreCase)
            ? directory.Parent?.FullName ?? directory.FullName
            : directory.FullName;
    }

    private static CliExecutionResult Failure(string status, int exitCode, string error) =>
        new() { Status = status, ExitCode = exitCode, StdErr = error };
}
