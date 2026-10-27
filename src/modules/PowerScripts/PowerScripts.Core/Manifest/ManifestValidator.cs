// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

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
        else if (!IsValidId(manifest.Id))
        {
            // The id is embedded unquoted into shell-verb commands and native context-menu args, and
            // is parsed by splitting on spaces, so restrict it to a safe, portable character set.
            errors.Add("'id' may only contain letters, digits, '.', '_' or '-'.");
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

        if (manifest.InputFormat is { } inputFormat && !ScriptIo.IsSupported(inputFormat))
        {
            errors.Add("'inputSchema' must describe none, text, files, image, or audio.");
        }

        if (manifest.OutputFormat is { } outputFormat && !ScriptIo.IsSupported(outputFormat))
        {
            errors.Add("'outputSchema' must describe none, text, files, image, or audio.");
        }

        if (manifest.Input is not null)
        {
            if (manifest.Input.Extensions.Count == 0)
            {
                errors.Add("file scripts must declare 'input.extensions'.");
            }

            if (manifest.Input is { MinFiles: < 0 })
            {
                errors.Add("'files.minItems' cannot be negative.");
            }

            if (manifest.Input.MaxFiles < 0 ||
                manifest.Input.MaxFiles < manifest.Input.MinFiles)
            {
                errors.Add("'input.maxFiles' must be omitted (unbounded) or >= minFiles.");
            }
        }

        if (manifest.Consumers is { } consumers &&
            (consumers.Count == 0 || consumers.Any(c => !IsValidConsumerId(c))))
        {
            errors.Add("'x-powerscript.consumers' must be omitted or contain non-empty module ids using letters, digits, '.', '_' or '-'.");
        }

        if (manifest.Execute is { } execute)
        {
            if (execute.Command is not { Count: > 0 } command ||
                command.Any(token => token is null || token.Contains('\0')) ||
                string.IsNullOrWhiteSpace(command[0]))
            {
                errors.Add("'x-execute.command' must contain an executable and valid argument tokens.");
            }

            if (execute.Stdin is not null && !execute.UsesJsonStdin)
            {
                errors.Add("'x-execute.stdin' must be 'json' or omitted.");
            }
        }

        ValidateParameters(manifest, errors);

        return errors;
    }

    public static bool IsValidConsumerId(string? id) => !string.IsNullOrWhiteSpace(id) && IsValidId(id);

    private static bool IsValidId(string id)
    {
        foreach (var c in id)
        {
            var ok = (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c is '.' or '_' or '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
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
                || type.Equals(ScriptParameter.ParameterTypeChoice, StringComparison.OrdinalIgnoreCase)
                || type.Equals(ScriptParameter.ParameterTypeFile, StringComparison.OrdinalIgnoreCase);
            if (!known)
            {
                errors.Add($"parameter '{p.Name}' has unknown type '{p.Type}' (expected string, int, bool, choice or file).");
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

                if (!string.IsNullOrEmpty(p.Default))
                {
                    if (!int.TryParse(p.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        errors.Add($"int parameter '{p.Name}' has a non-integer default '{p.Default}'.");
                    }
                    else if ((p.Min is { } minimum && number < minimum) || (p.Max is { } maximum && number > maximum))
                    {
                        errors.Add($"int parameter '{p.Name}' has a default outside its declared bounds.");
                    }
                }
            }

            if (p.IsBool && !string.IsNullOrEmpty(p.Default) && !bool.TryParse(p.Default, out _))
            {
                errors.Add($"bool parameter '{p.Name}' must have a true or false default.");
            }
        }
    }
}
