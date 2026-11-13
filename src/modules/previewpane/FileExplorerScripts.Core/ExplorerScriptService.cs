// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

public sealed class ExplorerScriptService
{
    public const string ConsumerId = "file-explorer";

    private readonly PowerScriptsClient client;

    /// <summary>The supplied client must be constructed with <see cref="ConsumerId"/>.</summary>
    public ExplorerScriptService(PowerScriptsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    public static void ValidateSelection(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count is < 1 or > LocalPaths.MaxCount)
        {
            throw new ArgumentException("Select between 1 and 1024 local regular files.", nameof(files));
        }

        foreach (var file in files)
        {
            LocalPaths.Validate(file, directory: false);
        }
    }

    public static bool IsApplicable(PowerScriptInfo script, IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(files);
        if (script.Input is not (PowerScriptIO.Files or PowerScriptIO.Image or PowerScriptIO.Audio))
        {
            return false;
        }

        var selection = files.ToArray();
        try
        {
            ValidateSelection(selection);
            return script.Input == PowerScriptIO.Files
                ? script.AcceptsFiles(selection)
                : selection.Length == 1 && MediaInput.Accepts(script, MediaInput.Probe(selection[0]));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<PowerScriptInfo>> GetApplicableScriptsAsync(
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        var selection = files.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSelection(selection);
        var scripts = await client.ListAsync(cancellationToken).ConfigureAwait(false);
        var media = selection.Length == 1 ? MediaInput.Probe(selection[0]) : null;
        return scripts.Where(script => script.Input switch
        {
            PowerScriptIO.Files => script.AcceptsFiles(selection),
            PowerScriptIO.Image or PowerScriptIO.Audio => MediaInput.Accepts(script, media),
            _ => false,
        }).ToArray();
    }

    public Task<IReadOnlyList<string>> ExecuteAsync(
        PowerScriptInfo script,
        IReadOnlyList<string> files,
        IReadOnlyDictionary<string, JsonElement> parameters,
        string destination,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(script, files, parameters, destination, progress: null, cancellationToken);

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        PowerScriptInfo script,
        IReadOnlyList<string> files,
        IReadOnlyDictionary<string, JsonElement> parameters,
        string destination,
        IProgress<ExplorerScriptStage>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(parameters);
        var selection = files.ToArray();
        var supplied = parameters.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(ExplorerScriptStage.Preparing);
        ValidateSelection(selection);
        LocalPaths.Validate(destination, directory: true);
        var scripts = await client.ListAsync(cancellationToken).ConfigureAwait(false);
        var current = scripts.SingleOrDefault(candidate => string.Equals(candidate.Id, script.Id, StringComparison.Ordinal));
        if (current is null || !IsApplicable(current, selection))
        {
            throw new InvalidOperationException("The script is no longer available for this selection. Refresh the Explorer menu and try again.");
        }

        var validated = ScriptParameters.ValidateTyped(current, supplied);
        var hasParameterBag = HasParameterBag(current);
        if (!hasParameterBag && validated.Count != 0)
        {
            throw new ArgumentException("The script's input schema no longer declares parameters. Refresh its parameters before running.", nameof(parameters));
        }

        var input = new PowerScriptTransformInput { Parameters = hasParameterBag ? validated : null };
        if (current.Input == PowerScriptIO.Files)
        {
            input.FilePaths = selection;
        }
        else
        {
            var media = await MediaInput.ReadAsync(current, selection[0], cancellationToken).ConfigureAwait(false);
            if (current.Input == PowerScriptIO.Image)
            {
                input.Image = media;
            }
            else
            {
                input.Audio = media;
            }
        }

        progress?.Report(ExplorerScriptStage.Running);
        var result = await client.TransformAsync(
            current.Id,
            input,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            progress?.Report(ExplorerScriptStage.Saving);
        }

        return await ResultExporter.ExportAsync(result, current, selection, destination, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasParameterBag(PowerScriptInfo script)
    {
        if (script.InputSchema is { ValueKind: JsonValueKind.Object } schema)
        {
            return schema.TryGetProperty("properties", out var properties) &&
                properties.ValueKind == JsonValueKind.Object &&
                properties.TryGetProperty("parameters", out _);
        }

        return script.Parameters.Count != 0;
    }
}
