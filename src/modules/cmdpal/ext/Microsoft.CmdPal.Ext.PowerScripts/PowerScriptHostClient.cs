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
/// Thin client over the PowerScripts host CLI. Reuses the same host so parameter prompts, trust and
/// runtime handling stay consistent with the other PowerScripts surfaces (Keyboard Manager, Explorer,
/// Advanced Paste). Parsing uses <see cref="JsonDocument"/> (no reflection) to stay AOT/trim friendly.
/// </summary>
internal static class PowerScriptHostClient
{
    private const string HostExeName = "PowerScripts.Host.exe";
    private const string CommandPaletteSurface = "commandPalette";

    /// <summary>Environment override pointing directly at the host executable (useful in dev builds).</summary>
    private const string HostPathEnvVar = "POWERSCRIPTS_HOST";

    /// <summary>Lists the scripts whose manifest declares the Command Palette surface.</summary>
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
    /// Runs a script by id via the host. The host handles the (optional) WinUI 3 parameter prompt,
    /// trust-on-first-use and the correct runtime, so nothing extra is needed here.
    /// </summary>
    public static void Run(string id)
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
                Arguments = $"run \"{id}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

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
            if (!DeclaresCommandPalette(element))
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

            scripts.Add(new PowerScriptInfo(id, string.IsNullOrEmpty(name) ? id : name, description));
        }

        return scripts;
    }

    private static bool DeclaresCommandPalette(JsonElement element)
    {
        if (!element.TryGetProperty("surfaces", out var surfaces) || surfaces.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return surfaces.EnumerateArray().Any(surface =>
            surface.ValueKind == JsonValueKind.String &&
            string.Equals(surface.GetString(), CommandPaletteSurface, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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
