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

        ValidateParameters(manifest, errors);

        return errors;
    }

    private static void ValidateParameters(PowerScriptManifest manifest, List<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in manifest.Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                errors.Add("each parameter must have a non-empty 'name'.");
                continue;
            }

            if (!seen.Add(p.Name))
            {
                errors.Add($"duplicate parameter name '{p.Name}'.");
            }

            var type = p.Type ?? string.Empty;
            var known = type.Equals(ScriptParameter.ParameterTypeString, StringComparison.OrdinalIgnoreCase)
                || type.Equals(ScriptParameter.ParameterTypeInt, StringComparison.OrdinalIgnoreCase)
                || type.Equals(ScriptParameter.ParameterTypeBool, StringComparison.OrdinalIgnoreCase)
                || type.Equals(ScriptParameter.ParameterTypeChoice, StringComparison.OrdinalIgnoreCase);
            if (!known)
            {
                errors.Add($"parameter '{p.Name}' has unknown type '{p.Type}' (expected string, int, bool or choice).");
                continue;
            }

            if (p.IsChoice)
            {
                if (p.Options.Count == 0)
                {
                    errors.Add($"choice parameter '{p.Name}' must declare at least one 'options' value.");
                }
                else if (!string.IsNullOrEmpty(p.Default) &&
                    !p.Options.Contains(p.Default, StringComparer.Ordinal))
                {
                    errors.Add($"choice parameter '{p.Name}' has a default '{p.Default}' that is not one of its options.");
                }
            }

            if (p.IsInt)
            {
                if (p.Min is { } min && p.Max is { } max && max < min)
                {
                    errors.Add($"int parameter '{p.Name}' has max < min.");
                }

                if (!string.IsNullOrEmpty(p.Default) && !int.TryParse(p.Default, out _))
                {
                    errors.Add($"int parameter '{p.Name}' has a non-integer default '{p.Default}'.");
                }
            }
        }
    }
}
