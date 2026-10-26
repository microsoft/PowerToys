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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    private int _catalogueGeneration;
    private string _loadedScriptsRoot;

    public async Task<string> CreateTemplateAsync(
        string projectName,
        string description,
        string runtime,
        string input,
        string output,
        string fileExtensions,
        int minFiles,
        int maxFiles)
    {
        var request = new JsonObject
        {
            ["projectName"] = projectName,
            ["description"] = description,
            ["fileExtensions"] = fileExtensions,
            ["runtime"] = runtime,
            ["input"] = input,
            ["output"] = output,
            ["minFiles"] = minFiles,
            ["maxFiles"] = maxFiles,
        };
        var result = JsonNode.Parse(await InvokeHostAsync(new[] { "template", "create", "--configure", "--root", ScriptsFolder }, request));
        var entry = result?["entryPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entry) || !File.Exists(entry) || !File.Exists(entry + ".tool.json"))
        {
            throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        }

        await ReloadScriptsAsync();
        return entry;
    }

    public async Task<PowerScriptSettingsSnapshot> GetScriptSettingsAsync(string entryPath) =>
        DeserializeSnapshot(await InvokeScriptSettingsHostAsync("get", entryPath));

    public async Task ResetScriptSettingsAsync(string entryPath)
    {
        DeserializeSnapshot(await InvokeScriptSettingsHostAsync("reset", entryPath));
        await ReloadScriptsAsync(preserveEditorOrder: true);
        Scripts.FirstOrDefault(script => string.Equals(script.EntryFullPath, entryPath, StringComparison.OrdinalIgnoreCase))?.InvalidateSettings();
    }

    public async Task ForgetScriptSettingsAsync(string entryPath)
    {
        await InvokeScriptSettingsHostAsync("forget", entryPath);
        await ReloadScriptsAsync(preserveEditorOrder: true);
        Scripts.FirstOrDefault(script => string.Equals(script.EntryFullPath, entryPath, StringComparison.OrdinalIgnoreCase))?.InvalidateSettings();
    }

    public async Task ReloadScriptsAsync(bool preserveEditorOrder = false)
    {
        var generation = Interlocked.Increment(ref _catalogueGeneration);
        var root = ScriptsFolder;
        var json = await InvokeHostAsync(new[] { "list", "--json", "--include-disabled", "--include-unconfigured", "--root", root });
        var scripts = JsonSerializer.Deserialize<List<PowerScriptListItem>>(json, JsonOptions)
            ?? throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        if (generation != Volatile.Read(ref _catalogueGeneration))
        {
            return;
        }

        var existing = string.Equals(_loadedScriptsRoot, root, StringComparison.OrdinalIgnoreCase)
            ? Scripts.GroupBy(script => script.EntryFullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, PowerScriptListItem>(StringComparer.OrdinalIgnoreCase);
        var refreshed = new List<PowerScriptListItem>(scripts.Count);
        foreach (var metadata in scripts)
        {
            var script = metadata;
            if (existing.TryGetValue(metadata.EntryFullPath, out var current))
            {
                current.UpdateMetadata(metadata);
                script = current;
            }

            ApplyMxcSettingsToScript(script);
            refreshed.Add(script);
        }

        Scripts.Clear();
        foreach (var script in refreshed)
        {
            Scripts.Add(script);
        }

        _loadedScriptsRoot = root;
        OnPropertyChanged(nameof(HasScripts));
        RefreshCatalogue(preserveEditorOrder);
    }

    private static PowerScriptSettingsSnapshot DeserializeSnapshot(string json)
    {
        var snapshot = JsonSerializer.Deserialize<PowerScriptSettingsSnapshot>(json, JsonOptions);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.EntryPath) ||
            snapshot.Recommended is null || snapshot.Effective is null || snapshot.Overrides is null)
        {
            throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        }

        return snapshot;
    }

    private Task<string> InvokeScriptSettingsHostAsync(string operation, string entryPath, JsonObject request = null) =>
        InvokeHostAsync(new[] { "script-settings", operation, "--entry", Path.GetFullPath(entryPath), "--root", ScriptsFolder }, request);

    private static async Task<string> InvokeHostAsync(IEnumerable<string> arguments, JsonObject request = null, TimeSpan? commandTimeout = null)
    {
        // Discovery can traverse development output folders, so keep it off the UI thread as well.
        var hostPath = await Task.Run(ResolveHostPath);
        if (string.IsNullOrEmpty(hostPath))
        {
            throw new FileNotFoundException(GetResourceString("PowerScripts_MxcSupportHostMissing"));
        }

        var startInfo = new ProcessStartInfo(hostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        using var timeout = new CancellationTokenSource(commandTimeout ?? TimeSpan.FromSeconds(30));

        // Drain both pipes concurrently: stderr must never block stdout or stdin.
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            if (request is not null)
            {
                await process.StandardInput.WriteAsync(request.ToJsonString().AsMemory(), timeout.Token);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? GetResourceString("PowerScripts_SettingsHostFailed")
                    : error.Trim());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                ManagedCommon.Logger.LogWarning($"PowerScripts Host diagnostics: {error.Trim()}");
            }

            return output;
        }
        catch (OperationCanceledException ex)
        {
            // Do not terminate a Host which may be completing an atomic settings write.
            throw new TimeoutException(GetResourceString("PowerScripts_SettingsTimeout"), ex);
        }
    }
}
