// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Host;

/// <summary>
/// Collects <see cref="ScriptParameter"/> values by launching the native WinUI 3 prompt
/// (<c>PowerScripts.PromptUI.exe</c>) as a separate process. Running the prompt out-of-process is
/// deliberate: Keyboard Manager launches this Host hidden and actively hides every window owned by
/// the Host process, so an in-process dialog would be hidden too. A child process's window is outside
/// KBM's reach, so the prompt shows reliably while the Host itself stays silent.
///
/// The manifest opt-in and pre-fill behavior match the previous in-process dialog: the prompt is only
/// shown when <see cref="PowerScriptManifest.PromptForParameters"/> is set, and each control is
/// pre-filled with the parameter default plus any <c>--set</c> override.
/// </summary>
internal static class PromptLauncher
{
    private const string PromptExeName = "PowerScripts.PromptUI.exe";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Shows the prompt for the given parameters, pre-filled from <paramref name="initialValues"/>.
    /// Returns <c>true</c> and the collected values when the user confirms, or <c>false</c> when the
    /// user cancels (in which case the script must not run). If the prompt helper can't be found or
    /// launched, it degrades to <c>true</c> with the initial values so the script still runs with its
    /// defaults/overrides (a hard failure would make the hotkey appear to do nothing).
    /// </summary>
    public static bool TryPrompt(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string?> initialValues,
        out Dictionary<string, string?> values)
    {
        values = new Dictionary<string, string?>(StringComparer.Ordinal);

        var promptExe = ResolvePromptExe();
        if (promptExe is null)
        {
            Console.Error.WriteLine($"run: '{PromptExeName}' not found; running with default parameters.");
            CopyInitial(manifest, initialValues, values);
            return true;
        }

        var specPath = Path.Combine(Path.GetTempPath(), $"powerscripts_prompt_{Guid.NewGuid():N}.json");
        var outPath = Path.Combine(Path.GetTempPath(), $"powerscripts_prompt_{Guid.NewGuid():N}.out.json");

        try
        {
            File.WriteAllText(specPath, BuildSpecJson(manifest, initialValues));

            var psi = new ProcessStartInfo
            {
                FileName = promptExe,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--spec");
            psi.ArgumentList.Add(specPath);
            psi.ArgumentList.Add("--out");
            psi.ArgumentList.Add(outPath);

            using var process = Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("run: failed to start the parameter prompt; running with default parameters.");
                CopyInitial(manifest, initialValues, values);
                return true;
            }

            process.WaitForExit();

            // Exit code 2 == user cancelled/closed the prompt. Anything non-zero other than a written
            // result is treated as cancel to be safe.
            if (process.ExitCode != 0 || !File.Exists(outPath))
            {
                return false;
            }

            var chosen = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(outPath), JsonOptions)
                         ?? new Dictionary<string, string?>();
            foreach (var (name, value) in chosen)
            {
                values[name] = value;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"run: parameter prompt failed ({ex.Message}); running with default parameters.");
            values.Clear();
            CopyInitial(manifest, initialValues, values);
            return true;
        }
        finally
        {
            TryDelete(specPath);
            TryDelete(outPath);
        }
    }

    private static void CopyInitial(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string?> initialValues,
        Dictionary<string, string?> values)
    {
        foreach (var p in manifest.Parameters)
        {
            values[p.Name] = initialValues.TryGetValue(p.Name, out var v) ? v : p.Default;
        }
    }

    private static string BuildSpecJson(PowerScriptManifest manifest, IReadOnlyDictionary<string, string?> initialValues)
    {
        var spec = new
        {
            title = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
            description = manifest.Description,
            parameters = manifest.Parameters.Select(p => new
            {
                name = p.Name,
                type = p.Type,
                label = p.Label,
                description = p.Description,
                options = p.Options,
                value = initialValues.TryGetValue(p.Name, out var v) ? v : p.Default,
                min = p.Min,
                max = p.Max,
            }).ToList(),
        };

        return JsonSerializer.Serialize(spec, JsonOptions);
    }

    /// <summary>
    /// Resolves the full path to <c>PowerScripts.PromptUI.exe</c>. Search order: explicit override env
    /// var, next to the Host exe, then (for in-repo Debug/Release runs) the prompt project's bin output
    /// discovered by walking up from the Host directory.
    /// </summary>
    private static string? ResolvePromptExe()
    {
        var overridePath = Environment.GetEnvironmentVariable("POWERSCRIPTS_PROMPTUI");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var hostDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(hostDir))
        {
            var beside = Path.Combine(hostDir, PromptExeName);
            if (File.Exists(beside))
            {
                return beside;
            }

            var dir = new DirectoryInfo(hostDir);
            while (dir is not null)
            {
                var promptBin = Path.Combine(dir.FullName, "src", "modules", "PowerScripts", "PowerScripts.PromptUI", "bin");
                if (Directory.Exists(promptBin))
                {
                    var found = Directory
                        .EnumerateFiles(promptBin, PromptExeName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(found))
                    {
                        return found;
                    }
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
