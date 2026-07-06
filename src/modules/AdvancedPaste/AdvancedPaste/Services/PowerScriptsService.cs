// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;

namespace AdvancedPaste.Services;

/// <summary>
/// Bridges Advanced Paste to PowerScripts. PowerScripts are user-authored scripts (PowerShell or
/// Python) that declare an <c>advancedPaste</c> surface and a transform contract
/// (<c>powerscript_from_&lt;input&gt;_to_&lt;output&gt;</c>). This service enumerates those scripts and runs
/// one as a clipboard transform by shelling the shared <c>PowerScripts.Host.exe</c>, so every surface
/// (a hotkey, the Explorer context menu, Advanced Paste) executes them through the same gated host.
/// </summary>
internal static class PowerScriptsService
{
    private const string HostExeName = "PowerScripts.Host.exe";
    private const string AdvancedPasteSurface = "advancedPaste";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>A PowerScript that opts into the Advanced Paste surface.</summary>
    public sealed record PowerScriptInfo(string Id, string Name, string Description, ClipboardFormat SupportedFormats);

    /// <summary>
    /// Enumerates PowerScripts that declare the <c>advancedPaste</c> surface and expose a transform
    /// contract. Returns an empty list when PowerScripts is disabled or the host is unavailable, so
    /// disabling PowerScripts makes its scripts disappear from Advanced Paste.
    /// </summary>
    public static IReadOnlyList<PowerScriptInfo> GetAdvancedPasteScripts()
    {
        if (!IsPowerScriptsEnabled())
        {
            return [];
        }

        string hostPath = ResolveHostPath();
        if (string.IsNullOrEmpty(hostPath))
        {
            return [];
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
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return [];
            }

            string json = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var scripts = JsonSerializer.Deserialize<List<HostScript>>(json, JsonOptions) ?? [];

            return scripts
                .Where(s => s.Surfaces is not null && s.Surfaces.Contains(AdvancedPasteSurface, StringComparer.OrdinalIgnoreCase))
                .Where(s => s.Transform is not null)
                .Select(s => new PowerScriptInfo(s.Id, s.Name, s.Description ?? string.Empty, MapInputFormat(s.Transform.InputFormat)))
                .ToList();
        }
        catch (Exception)
        {
            // A missing/failed host simply yields no Advanced Paste PowerScripts.
            return [];
        }
    }

    /// <summary>
    /// Runs the given PowerScript as a text transform: sends <c>{"text": ...}</c> on stdin and returns
    /// the <c>text</c> field of the host's JSON result.
    /// </summary>
    public static async Task<string> TransformTextAsync(string scriptId, string inputText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scriptId))
        {
            return string.Empty;
        }

        string hostPath = ResolveHostPath();
        if (string.IsNullOrEmpty(hostPath))
        {
            return string.Empty;
        }

        var psi = new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = $"transform {scriptId}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start PowerScripts host.");

        var payload = JsonSerializer.Serialize(new TransformPayload { Text = inputText ?? string.Empty });
        await process.StandardInput.WriteAsync(payload);
        process.StandardInput.Close();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        try
        {
            var result = JsonSerializer.Deserialize<TransformPayload>(output, JsonOptions);
            return result?.Text ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static ClipboardFormat MapInputFormat(string inputFormat) =>
        inputFormat?.ToLowerInvariant() switch
        {
            "html" => ClipboardFormat.Html,
            "image" => ClipboardFormat.Image,
            "audio" => ClipboardFormat.Audio,
            "video" => ClipboardFormat.Video,
            "files" or "file" => ClipboardFormat.File,

            // "text" and "none" (no clipboard input needed) both surface for text on the clipboard.
            _ => ClipboardFormat.Text,
        };

    /// <summary>
    /// Reads <c>enabled.PowerScripts</c> from the PowerToys settings. Mirrors the host's own gate so a
    /// disabled module never surfaces scripts. A missing settings file (dev/test) is treated as enabled;
    /// an absent key means the module is off by default.
    /// </summary>
    private static bool IsPowerScriptsEnabled()
    {
        if (Environment.GetEnvironmentVariable("POWERSCRIPTS_IGNORE_ENABLED") == "1")
        {
            return true;
        }

        try
        {
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "settings.json");

            if (!File.Exists(settingsPath))
            {
                return true;
            }

            using var stream = File.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("enabled", out var enabled) &&
                enabled.TryGetProperty("PowerScripts", out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            return false;
        }
        catch (Exception)
        {
            // A corrupt/unreadable settings file falls back to allowing the module (matches host behavior).
            return true;
        }
    }

    private static string ResolveHostPath()
    {
        string moduleDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "PowerScripts");

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, HostExeName),
            Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
            Path.Combine(moduleDir, HostExeName),
        };

        // Prototype dev fallback: when running an in-repo build the host isn't copied next to Advanced
        // Paste, so walk up from the base directory and probe the host project's bin output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var hostBin = Path.Combine(dir.FullName, "src", "modules", "PowerScripts", "PowerScripts.Host", "bin", config);
                if (Directory.Exists(hostBin))
                {
                    var found = Directory.EnumerateFiles(hostBin, HostExeName, SearchOption.AllDirectories).FirstOrDefault();
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

    private sealed class HostScript
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<string> Surfaces { get; set; }

        public TransformContract Transform { get; set; }
    }

    private sealed class TransformContract
    {
        public string Function { get; set; }

        public string InputFormat { get; set; }

        public string OutputFormat { get; set; }
    }

    private sealed class TransformPayload
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
