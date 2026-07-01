// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Validates a parsed manifest. Returns human-readable errors rather than throwing so the registry
/// can skip a single bad script without failing the whole catalogue.
///
/// A script's <c>id</c> is its portable identity and is intentionally decoupled from the folder it
/// happens to live in: this lets a script keep a stable id when it is shared, downloaded from a
/// community catalogue, or dropped into a differently-named folder to avoid a local name clash.
/// Uniqueness of ids across the catalogue is enforced by the registry, not here.
/// </summary>
public static class ManifestValidator
{
    public static IReadOnlyList<string> Validate(PowerScriptManifest manifest, string folderName)
    {
        _ = folderName;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("'id' is required.");
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
