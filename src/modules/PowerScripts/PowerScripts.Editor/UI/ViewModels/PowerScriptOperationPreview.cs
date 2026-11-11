// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace PowerScripts.UI.ViewModels;

/// <summary>Owns one unpublished package and, if the user approves its run, only that package's approval.</summary>
internal sealed class PowerScriptOperationPreview
{
    private readonly IPowerScriptOperationPreviewBackend _backend;
    private readonly string _parent;
    private readonly Action<Exception> _reportCleanupError;
    private readonly TimeSpan _timeout;

    public PowerScriptOperationPreview(
        IPowerScriptOperationPreviewBackend backend,
        string parent,
        Action<Exception> reportCleanupError,
        TimeSpan? timeout = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent ?? throw new ArgumentNullException(nameof(parent))));
        _reportCleanupError = reportCleanupError ?? throw new ArgumentNullException(nameof(reportCleanupError));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<PowerScriptTransformResult> RunAsync(
        PowerScriptOperationTemplate template,
        JsonObject operation,
        JsonArray parameters,
        IReadOnlyDictionary<string, JsonElement> parameterValues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(template.UnavailableReason))
        {
            // Do not publish even a private package or prompt for trust when the catalogue already
            // establishes that the effective sandbox cannot provide a reliable result.
            return new PowerScriptTransformResult { ExitCode = 125, StdErr = template.UnavailableReason };
        }

        if (string.IsNullOrWhiteSpace(template.CommandId) ||
            !string.Equals(template.CommandId, operation["commandId"]?.GetValue<string>(), StringComparison.Ordinal))
        {
            throw InvalidResponse();
        }

        var request = new JsonObject
        {
            ["projectName"] = template.Name,
            ["description"] = template.Description,
            ["operation"] = operation.DeepClone(),
            ["parameters"] = parameters?.DeepClone(),
        };
        var input = new PowerScriptTransformInput
        {
            // Clone before the first await: later form edits cannot change an in-flight test.
            Parameters = parameterValues?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal),
        };
        var root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
        string id = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            Directory.CreateDirectory(root);
            var entry = await _backend.CreateAsync(root, request, deadline.Token);
            id = await ReadCreatedIdAsync(root, entry, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            return await _backend.RunAsync(root, id, input, deadline.Token);
        }
        finally
        {
            // Cleanup is independent of the cancelled run token. Never touch installed scripts,
            // other approvals, the configured scripts folder, or any consumer bindings.
            if (id is not null)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _backend.RevokeAsync(root, id, cleanup.Token);
                }
                catch (Exception ex)
                {
                    _reportCleanupError(ex);
                }
            }

            try
            {
                await Task.Run(() => DeleteOwnedDirectory(root), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _reportCleanupError(ex);
            }
        }
    }

    private static async Task<string> ReadCreatedIdAsync(string root, string entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            throw InvalidResponse();
        }

        var fullEntry = Path.GetFullPath(entry);
        var package = Path.GetDirectoryName(fullEntry);
        if (!string.Equals(Path.GetDirectoryName(package), root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(fullEntry), "action.json", StringComparison.Ordinal) ||
            !File.Exists(fullEntry) ||
            (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(package) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(fullEntry) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(fullEntry + ".tool.json") & FileAttributes.ReparsePoint) != 0)
        {
            throw InvalidResponse();
        }

        var id = Path.GetFileName(package);
        using var descriptor = JsonDocument.Parse(await File.ReadAllTextAsync(fullEntry + ".tool.json", cancellationToken));
        if (descriptor.RootElement.ValueKind != JsonValueKind.Object ||
            !descriptor.RootElement.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String || !string.Equals(name.GetString(), id, StringComparison.Ordinal))
        {
            throw InvalidResponse();
        }

        return id;
    }

    private void DeleteOwnedDirectory(string root)
    {
        // Verify the exact absolute target immediately before recursive deletion on Windows.
        var target = Path.GetFullPath(root);
        if (!string.Equals(Path.GetDirectoryName(target), _parent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(target), "N", out _))
        {
            throw InvalidResponse();
        }

        if (Directory.Exists(target))
        {
            CheckOwnedTree(new DirectoryInfo(target));
            Directory.Delete(target, recursive: true);
        }
    }

    private static void CheckOwnedTree(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw InvalidResponse();
        }

        foreach (var item in directory.EnumerateFileSystemInfos())
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw InvalidResponse();
            }

            if (item is DirectoryInfo child)
            {
                CheckOwnedTree(child);
            }
        }
    }

    private static InvalidDataException InvalidResponse() =>
        new(PowerScriptsViewModel.GetResourceString("PowerScripts_SettingsInvalidResponse"));
}
