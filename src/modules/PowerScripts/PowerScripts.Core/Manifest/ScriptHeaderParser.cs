// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Builds a <see cref="PowerScriptManifest"/> from directives embedded in a script file's leading
/// comment block, so a single self-contained file can describe a PowerScript with no separate
/// <c>manifest.json</c> — the Raycast-style authoring model that makes a script easy to share as one
/// file. Directives are '#'-comment lines of the form <c># @powerscript.&lt;key&gt; &lt;value&gt;</c>:
///
/// <code>
/// # @powerscript.id          copy-as-unc
/// # @powerscript.name        Copy as UNC path
/// # @powerscript.description  Resolve a mapped drive to its UNC path and copy it.
/// # @powerscript.kind        file
/// # @powerscript.extensions  *
/// # @powerscript.capability  clipboard
/// # @powerscript.param       name=name type=string label="Name" default=World
/// </code>
///
/// Only '#'-comment runtimes (PowerShell, Python) are supported, which covers both current runtimes.
/// Repeatable keys (<c>extensions</c>, <c>capability</c>, <c>surface</c>, <c>param</c>) may appear
/// multiple times or carry a comma/space-separated list. Returns <c>null</c> when the file declares
/// no <c>@powerscript.*</c> directives, so an unrelated helper script is left untouched.
/// </summary>
public static class ScriptHeaderParser
{
    private const string DirectivePrefix = "@powerscript.";

    /// <summary>
    /// Parses the leading comment block of <paramref name="filePath"/>. Returns a manifest whose
    /// <see cref="PowerScriptManifest.Entry"/> is the file name and whose runtime is inferred from the
    /// extension when not stated, or <c>null</c> when the file has no PowerScript directives.
    /// </summary>
    public static PowerScriptManifest? TryParseFile(string filePath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (Exception)
        {
            return null;
        }

        var directives = ExtractDirectives(lines);
        if (directives.Count == 0)
        {
            return null;
        }

        var manifest = new PowerScriptManifest
        {
            Entry = Path.GetFileName(filePath),
            Runtime = InferRuntime(filePath),
        };

        Apply(manifest, directives);
        return manifest;
    }

    /// <summary>Infers the runtime from the file extension (.py =&gt; Python, else PowerShell).</summary>
    public static ScriptRuntime InferRuntime(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".py", StringComparison.OrdinalIgnoreCase)
            ? ScriptRuntime.Python
            : ScriptRuntime.PowerShell;

    /// <summary>
    /// Pulls the ordered <c>(key, value)</c> directives out of the file's leading comment block. Blank
    /// lines are allowed; the first non-comment line ends the header.
    /// </summary>
    private static List<KeyValuePair<string, string>> ExtractDirectives(IEnumerable<string> lines)
    {
        var result = new List<KeyValuePair<string, string>>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith('#'))
            {
                // First real line of code ends the header block.
                break;
            }

            var comment = line.TrimStart('#').Trim();
            if (!comment.StartsWith(DirectivePrefix, StringComparison.OrdinalIgnoreCase))
            {
                // A comment (e.g. the license header) that is not a PowerScript directive.
                continue;
            }

            var rest = comment.Substring(DirectivePrefix.Length);
            var split = IndexOfWhitespace(rest);
            string key;
            string value;
            if (split < 0)
            {
                key = rest;
                value = string.Empty;
            }
            else
            {
                key = rest.Substring(0, split);
                value = rest.Substring(split + 1).Trim();
            }

            if (key.Length > 0)
            {
                result.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        return result;
    }

    private static void Apply(PowerScriptManifest manifest, List<KeyValuePair<string, string>> directives)
    {
        foreach (var (rawKey, value) in directives)
        {
            switch (rawKey.ToLowerInvariant())
            {
                case "schemaversion":
                    if (int.TryParse(value, out var schema))
                    {
                        manifest.SchemaVersion = schema;
                    }

                    break;
                case "id":
                    manifest.Id = value.Trim();
                    break;
                case "name":
                    manifest.Name = value.Trim();
                    break;
                case "description":
                case "desc":
                    manifest.Description = value.Trim();
                    break;
                case "icon":
                    manifest.Icon = value.Trim();
                    break;
                case "publisher":
                case "author":
                    manifest.Publisher = value.Trim();
                    break;
                case "version":
                    manifest.Version = value.Trim();
                    break;
                case "source":
                    manifest.Source = value.Trim();
                    break;
                case "kind":
                    if (TryParseKind(value, out var kind))
                    {
                        manifest.Kind = kind;
                    }

                    break;
                case "runtime":
                    if (TryParseRuntime(value, out var runtime))
                    {
                        manifest.Runtime = runtime;
                    }

                    break;
                case "entry":
                    manifest.Entry = value.Trim();
                    break;
                case "elevation":
                    manifest.Elevation = value.Trim();
                    break;
                case "promptforparameters":
                case "prompt":
                    manifest.PromptForParameters = ParseBool(value);
                    break;
                case "extension":
                case "extensions":
                    (manifest.Input ??= new ScriptInput()).Extensions.AddRange(SplitList(value));
                    break;
                case "minfiles":
                    if (int.TryParse(value, out var min))
                    {
                        (manifest.Input ??= new ScriptInput()).MinFiles = min;
                    }

                    break;
                case "maxfiles":
                    if (int.TryParse(value, out var max))
                    {
                        (manifest.Input ??= new ScriptInput()).MaxFiles = max;
                    }

                    break;
                case "output":
                    if (TryParseOutput(value, out var output))
                    {
                        (manifest.Output ??= new ScriptOutput()).Type = output;
                    }

                    break;
                case "outputextension":
                    (manifest.Output ??= new ScriptOutput()).Extension = value.Trim();
                    break;
                case "capability":
                case "capabilities":
                    manifest.Capabilities.AddRange(SplitList(value));
                    break;
                case "surface":
                case "surfaces":
                    manifest.Surfaces.AddRange(SplitList(value));
                    break;
                case "param":
                case "parameter":
                    var parameter = ParseParameter(value);
                    if (parameter is not null)
                    {
                        manifest.Parameters.Add(parameter);
                    }

                    break;
                default:
                    // Unknown directive: ignored so newer keys don't break older readers.
                    break;
            }
        }
    }

    private static bool TryParseKind(string value, out ScriptKind kind)
    {
        // "action" is accepted as a friendlier alias for a system (no file I/O) script.
        if (value.Equals("system", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("action", StringComparison.OrdinalIgnoreCase))
        {
            kind = ScriptKind.System;
            return true;
        }

        // "content" / "object" are accepted aliases for a file (input-driven) script.
        if (value.Equals("file", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("content", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("object", StringComparison.OrdinalIgnoreCase))
        {
            kind = ScriptKind.File;
            return true;
        }

        kind = ScriptKind.System;
        return false;
    }

    private static bool TryParseRuntime(string value, out ScriptRuntime runtime)
    {
        if (value.Equals("python", StringComparison.OrdinalIgnoreCase))
        {
            runtime = ScriptRuntime.Python;
            return true;
        }

        if (value.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            runtime = ScriptRuntime.PowerShell;
            return true;
        }

        runtime = ScriptRuntime.PowerShell;
        return false;
    }

    private static bool TryParseOutput(string value, out ScriptOutputType output)
    {
        if (value.Equals("convertedFile", StringComparison.OrdinalIgnoreCase))
        {
            output = ScriptOutputType.ConvertedFile;
            return true;
        }

        if (value.Equals("sideEffect", StringComparison.OrdinalIgnoreCase))
        {
            output = ScriptOutputType.SideEffect;
            return true;
        }

        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            output = ScriptOutputType.None;
            return true;
        }

        output = ScriptOutputType.None;
        return false;
    }

    private static bool ParseBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("1", StringComparison.Ordinal) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Splits a directive value on commas and/or whitespace, dropping empty entries.</summary>
    private static IEnumerable<string> SplitList(string value) =>
        value.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Parses a <c># @powerscript.param</c> value. The name and type may be positional (first two
    /// tokens) or given as <c>name=</c>/<c>type=</c>; remaining <c>key=value</c> tokens set label,
    /// description, default, options (comma-separated), min and max.
    /// </summary>
    private static ScriptParameter? ParseParameter(string value)
    {
        var tokens = TokenizeQuoted(value);
        if (tokens.Count == 0)
        {
            return null;
        }

        var parameter = new ScriptParameter();
        var positional = 0;

        foreach (var token in tokens)
        {
            var eq = token.IndexOf('=');
            if (eq < 0)
            {
                // Positional: first is name, second is type.
                if (positional == 0)
                {
                    parameter.Name = token;
                }
                else if (positional == 1)
                {
                    parameter.Type = token;
                }

                positional++;
                continue;
            }

            var key = token.Substring(0, eq).Trim().ToLowerInvariant();
            var val = token.Substring(eq + 1).Trim();
            switch (key)
            {
                case "name":
                    parameter.Name = val;
                    break;
                case "type":
                    parameter.Type = val;
                    break;
                case "label":
                    parameter.Label = val;
                    break;
                case "description":
                case "desc":
                    parameter.Description = val;
                    break;
                case "default":
                    parameter.Default = val;
                    break;
                case "options":
                    parameter.Options.AddRange(val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "min":
                    if (int.TryParse(val, out var min))
                    {
                        parameter.Min = min;
                    }

                    break;
                case "max":
                    if (int.TryParse(val, out var max))
                    {
                        parameter.Max = max;
                    }

                    break;
                default:
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(parameter.Name) ? null : parameter;
    }

    /// <summary>Splits on whitespace while keeping double-quoted spans intact (quotes are stripped).</summary>
    private static List<string> TokenizeQuoted(string value)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var c in value)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static int IndexOfWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
