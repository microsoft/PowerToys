// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;

namespace PowerScripts.Core.Security;

/// <summary>The result of probing whether this host can run PowerScripts through MXC.</summary>
public sealed class MxcPlatformSupportResult
{
    public bool IsSupported { get; init; }

    public string Reason { get; init; } = string.Empty;

    public int WindowsBuild { get; init; }

    public string ExecutorPath { get; init; } = string.Empty;

    public string Tier { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>Checks MXC's supported Windows floor and performs the executor's read-only host probe.</summary>
public static class MxcPlatformSupport
{
    public const int MinimumWindowsBuild = 26100;

    private const int ProbeTimeoutMs = 10000;

    public static MxcPlatformSupportResult Probe(MxcSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var build = Environment.OSVersion.Version.Build;
        if (!OperatingSystem.IsWindows() || build < MinimumWindowsBuild)
        {
            return Unsupported(
                build,
                $"MXC requires Windows 11, version 24H2 (build {MinimumWindowsBuild}) or newer.");
        }

        var executorPath = MxcProcessRunner.ResolveExecutor(settings);
        if (string.IsNullOrWhiteSpace(executorPath))
        {
            return Unsupported(build, "wxc-exec.exe was not found.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executorPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--probe");

            var result = ProcessRunner.SecureLauncher.Run(startInfo, standardInput: null, ProbeTimeoutMs);
            return EvaluateProbe(build, executorPath, result.ExitCode, result.StdOut, result.StdErr);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidOperationException or
            System.ComponentModel.Win32Exception or JsonException)
        {
            return Unsupported(build, $"The MXC support probe failed: {ex.Message}", executorPath);
        }
    }

    internal static MxcPlatformSupportResult EvaluateProbe(
        int windowsBuild,
        string executorPath,
        int exitCode,
        string standardOutput,
        string standardError)
    {
        if (windowsBuild < MinimumWindowsBuild)
        {
            return Unsupported(
                windowsBuild,
                $"MXC requires Windows 11, version 24H2 (build {MinimumWindowsBuild}) or newer.",
                executorPath);
        }

        if (exitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(standardError)
                ? $"exit code {exitCode}"
                : standardError.Trim();
            return Unsupported(windowsBuild, $"The MXC support probe reported {detail}.", executorPath);
        }

        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;
            var tier = root.TryGetProperty("tier", out var tierProperty) &&
                       tierProperty.ValueKind == JsonValueKind.String
                ? tierProperty.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(tier) ||
                string.Equals(tier, "unsupported", StringComparison.OrdinalIgnoreCase))
            {
                return Unsupported(windowsBuild, "MXC did not report an available containment tier.", executorPath);
            }

            var warnings = root.TryGetProperty("warnings", out var warningsProperty) &&
                           warningsProperty.ValueKind == JsonValueKind.Array
                ? warningsProperty.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray()
                : Array.Empty<string>();

            return new MxcPlatformSupportResult
            {
                IsSupported = true,
                Reason = warnings.Length == 0
                    ? $"MXC is available through the {tier} tier."
                    : $"MXC is available through the {tier} tier. {string.Join(" ", warnings)}",
                WindowsBuild = windowsBuild,
                ExecutorPath = executorPath,
                Tier = tier,
                Warnings = warnings,
            };
        }
        catch (JsonException)
        {
            return Unsupported(windowsBuild, "The MXC support probe returned invalid JSON.", executorPath);
        }
    }

    private static MxcPlatformSupportResult Unsupported(
        int windowsBuild,
        string reason,
        string executorPath = "") =>
        new()
        {
            IsSupported = false,
            Reason = reason,
            WindowsBuild = windowsBuild,
            ExecutorPath = executorPath,
        };
}
