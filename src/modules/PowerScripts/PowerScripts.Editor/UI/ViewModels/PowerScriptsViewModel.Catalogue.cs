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
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

public partial class PowerScriptsViewModel
{
    private static readonly string[] OperationTemplateListArguments = new[] { "template", "list" };
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
        int maxFiles,
        JsonArray parameters = null,
        JsonObject operation = null)
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
            ["parameters"] = parameters,
        };
        if (operation is not null)
        {
            request["operation"] = operation;
        }

        var result = JsonNode.Parse(await InvokeHostAsync(new[] { "template", "create", "--configure", "--root", ScriptsFolder }, request));
        var entry = result?["entryPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(entry) || !File.Exists(entry) || !File.Exists(entry + ".tool.json"))
        {
            throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        }

        await ReloadScriptsAsync();
        return entry;
    }

    public async Task<IReadOnlyList<PowerScriptOperationTemplate>> LoadOperationTemplatesAsync()
    {
        var json = await InvokeHostAsync(OperationTemplateListArguments);
        var templates = JsonSerializer.Deserialize<List<PowerScriptOperationTemplate>>(json, JsonOptions)
            ?? throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        var operations = templates.Where(template => !string.IsNullOrWhiteSpace(template.CommandId)).ToArray();
        if (operations.Any(template => string.IsNullOrWhiteSpace(template.Id) || string.IsNullOrWhiteSpace(template.Name) ||
                template.Parameters is null || template.Parameters.Any(parameter => parameter is null ||
                    string.IsNullOrWhiteSpace(parameter.Name) || parameter.Schema is null)) ||
            operations.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() != operations.Length)
        {
            throw new InvalidDataException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        }

        return operations;
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

    private static async Task<string> InvokeHostAsync(IEnumerable<string> arguments, JsonObject request = null, TimeSpan? commandTimeout = null)
    {
        var result = await InvokeHostCommandAsync(arguments, request, commandTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StdErr)
                ? GetResourceString("PowerScripts_SettingsHostFailed")
                : result.StdErr.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            ManagedCommon.Logger.LogWarning($"PowerScripts Host diagnostics: {result.StdErr.Trim()}");
        }

        return result.StdOut;
    }

    private static async Task<PowerScriptRunResult> InvokeHostCommandAsync(
        IEnumerable<string> arguments,
        JsonObject request = null,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        // Discovery can traverse development output folders, so keep it off the UI thread as well.
        var hostPath = await Task.Run(ResolveHostPath, cancellationToken);
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

        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(GetResourceString("PowerScripts_SettingsInvalidResponse"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(commandTimeout ?? TimeSpan.FromSeconds(30));

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
            return new PowerScriptRunResult
            {
                ExitCode = process.ExitCode,
                StdOut = output,
                StdErr = error,
            };
        }
        catch (OperationCanceledException ex)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(shutdown.Token);
            }
            catch (Exception shutdownError) when (shutdownError is InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
            {
                ManagedCommon.Logger.LogError("Unable to confirm that a cancelled PowerScripts Host command exited.", shutdownError);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(GetResourceString("PowerScripts_SettingsTimeout"), ex);
        }
    }
}
