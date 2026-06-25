// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Validates a parsed manifest. Returns human-readable errors rather than throwing so the registry
/// can skip a single bad script without failing the whole catalogue.
/// </summary>
public static class ManifestValidator
{
    public static IReadOnlyList<string> Validate(PowerScriptManifest manifest, string folderName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("'id' is required.");
        }
        else if (!string.Equals(manifest.Id, folderName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"'id' ('{manifest.Id}') must match the folder name ('{folderName}').");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("'name' is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            errors.Add("'entry' is required.");
        }
        else if (!string.IsNullOrEmpty(manifest.FolderPath) && !File.Exists(manifest.EntryFullPath))
        {
            errors.Add($"entry script not found: '{manifest.Entry}'.");
        }

        if (manifest.Kind == ScriptKind.File)
        {
            if (manifest.Input is null || manifest.Input.Extensions.Count == 0)
            {
                errors.Add("file scripts must declare 'input.extensions'.");
            }

            if (manifest.Input is { MinFiles: < 1 })
            {
                errors.Add("'input.minFiles' must be at least 1.");
            }

            if (manifest.Input is { MaxFiles: > 0 } input && input.MaxFiles < input.MinFiles)
            {
                errors.Add("'input.maxFiles' must be 0 (unbounded) or >= minFiles.");
            }
        }

        return errors;
    }
}
