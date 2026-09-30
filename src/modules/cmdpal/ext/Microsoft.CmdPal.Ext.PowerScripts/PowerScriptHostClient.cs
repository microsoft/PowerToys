// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// Thin client over the PowerScripts host CLI. Parameter presentation belongs to this extension;
/// trust and runtime handling remain centralized in the Host.
/// </summary>
internal static class PowerScriptHostClient
{
    private const string HostExeName = "PowerScripts.Host.exe";

    /// <summary>Environment override pointing directly at the host executable (useful in dev builds).</summary>
    private const string HostPathEnvVar = "POWERSCRIPTS_HOST";

    /// <summary>Lists the no-input action scripts that Command Palette can invoke.</summary>
    public static IReadOnlyList<PowerScriptInfo> ListCommandPaletteScripts()
    {
        var hostPath = ResolveHostPath();
        if (string.IsNullOrEmpty(hostPath))
        {
            return Array.Empty<PowerScriptInfo>();
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = "list --json",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return Array.Empty<PowerScriptInfo>();
            }

            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return Parse(json);
        }
        catch (Exception)
        {
            // Prototype: a missing or failing host simply yields no Command Palette entries.
            return Array.Empty<PowerScriptInfo>();
        }
    }

    /// <summary>
    /// Runs a script by id via the host with values collected by Command Palette.
    /// </summary>
    public static void Run(string id, IReadOnlyDictionary<string, string?>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var hostPath = ResolveHostPath();
        if (string.IsNullOrEmpty(hostPath))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(id);
            if (parameters is not null)
            {
                foreach (var (name, value) in parameters)
                {
                    psi.ArgumentList.Add("--set");
                    psi.ArgumentList.Add($"{name}={value ?? string.Empty}");
                }
            }

            Process.Start(psi);
        }
        catch (Exception)
        {
            // Prototype: best-effort launch.
        }
    }

    private static List<PowerScriptInfo> Parse(string json)
    {
        var scripts = new List<PowerScriptInfo>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return scripts;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return scripts;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!AcceptsNoInput(element))
            {
                continue;
            }

            var id = GetString(element, "id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var name = GetString(element, "name");
            var description = GetString(element, "description");

            scripts.Add(new PowerScriptInfo(
                id,
                string.IsNullOrEmpty(name) ? id : name,
                description,
                ParseParameters(element)));
        }

        return scripts;
    }

    private static bool AcceptsNoInput(JsonElement element)
    {
        if (!element.TryGetProperty("io", out var io) ||
            io.ValueKind != JsonValueKind.Object ||
            !io.TryGetProperty("input", out var input) ||
            input.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return string.Equals(input.GetString(), "none", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static IReadOnlyList<PowerScriptParameterInfo> ParseParameters(JsonElement element)
    {
        if (!element.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PowerScriptParameterInfo>();
        }

        var results = new List<PowerScriptParameterInfo>();
        foreach (var parameter in parameters.EnumerateArray())
        {
            var name = GetString(parameter, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            results.Add(new PowerScriptParameterInfo(
                name,
                GetString(parameter, "type"),
                parameter.TryGetProperty("isRequired", out var required) && required.ValueKind == JsonValueKind.True,
                GetNullableString(parameter, "label"),
                GetNullableString(parameter, "description"),
                GetNullableString(parameter, "default"),
                GetStringArray(parameter, "options"),
                GetNullableInt(parameter, "min"),
                GetNullableInt(parameter, "max")));
        }

        return results;
    }

    private static string? GetNullableString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!)
                .ToArray()
            : Array.Empty<string>();

    private static int? GetNullableInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;

    private static string ResolveHostPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable(HostPathEnvVar);
        if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, HostExeName),
            Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
        };

        // Prototype dev fallback: when running an in-repo build the host isn't copied next to CmdPal,
        // so walk up from the base directory and probe the host project's bin output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var hostBin = Path.Combine(
                    dir.FullName,
                    "src",
                    "modules",
                    "PowerScripts",
                    "PowerScripts.Host",
                    "bin",
                    config);

                if (Directory.Exists(hostBin))
                {
                    var found = Directory
                        .EnumerateFiles(hostBin, HostExeName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(found))
                    {
                        candidates.Add(found);
                    }
                }
            }

            dir = dir.Parent;
        }

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }
}
