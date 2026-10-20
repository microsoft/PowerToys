// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Builds a <see cref="PowerScriptManifest"/> from an explicit <c>&lt;script&gt;.tool.json</c>
/// descriptor (see <see cref="ToolDescriptor"/>). This is the "write the MCP Tool shape by hand"
/// authoring path: language-agnostic (works for <c>.py</c>, <c>.ps1</c>, <c>.cmd</c>, <c>.sh</c>,
/// even ones that can't carry a header) and the single artifact an author publishes.
///
/// The descriptor's standard MCP fields map onto the manifest the rest of PowerScripts already
/// understands: <c>name</c> -&gt; id, <c>description</c> -&gt; description, and each scalar
/// <c>inputSchema</c> property -&gt; a typed <see cref="ScriptParameter"/> (a file-typed property,
/// carrying a <c>contentMediaType</c>, marks the script as file-driven instead). The
/// <c>x-execute</c> block is kept on the manifest so the executor can launch the script.
/// </summary>
public static class ToolDescriptorParser
{
    /// <summary>The sidecar filename suffix: a descriptor for <c>foo.py</c> is <c>foo.py.tool.json</c>.</summary>
    public const string DescriptorSuffix = ".tool.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>True when <paramref name="path"/> is a <c>*.tool.json</c> descriptor file.</summary>
    public static bool IsDescriptorFile(string path) =>
        path.EndsWith(DescriptorSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a descriptor file and returns a manifest whose <see cref="PowerScriptManifest.Entry"/>
    /// is the sibling script (the descriptor filename minus <c>.tool.json</c>) and whose runtime is
    /// inferred from that script's extension. Returns <c>null</c> when the file is not a descriptor,
    /// cannot be read/parsed, or declares no tool <c>name</c>.
    /// </summary>
    public static PowerScriptManifest? TryParseFile(string descriptorPath)
    {
        if (!IsDescriptorFile(descriptorPath))
        {
            return null;
        }

        ToolDescriptor? descriptor;
        try
        {
            descriptor = JsonSerializer.Deserialize<ToolDescriptor>(File.ReadAllText(descriptorPath), JsonOptions);
        }
        catch (Exception)
        {
            // A malformed descriptor is skipped here; the registry surfaces it as a load error when it
            // still can't produce a valid manifest.
            return null;
        }

        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.Name))
        {
            return null;
        }

        // The script sits next to the descriptor and shares its stem: foo.py.tool.json -> foo.py.
        var entry = Path.GetFileName(descriptorPath[..^DescriptorSuffix.Length]);

        var extension = descriptor.PowerScript;
        var manifest = new PowerScriptManifest
        {
            Id = descriptor.Name!.Trim(),
            Name = FirstNonEmpty(descriptor.Title, descriptor.Annotations?.Title, descriptor.Name)!.Trim(),
            Description = descriptor.Description?.Trim() ?? string.Empty,
            Entry = entry,
            Runtime = ResolveRuntime(extension?.Runtime, descriptorPath, entry),
            Execute = descriptor.Execute,
            Publisher = extension?.Publisher,
            Version = extension?.Version,
            Source = extension?.Source,
            Icon = extension?.Icon,
        };

        if (extension?.Capabilities is { Count: > 0 } capabilities)
        {
            manifest.Capabilities.AddRange(capabilities);
        }

        if (extension?.Mxc?.RecommendedPolicies is { } recommendedPolicies)
        {
            manifest.Mxc.RecommendedPolicies = MxcPolicies.Normalize(recommendedPolicies);
        }

        if (ScriptIo.TryParse(extension?.Input) is { } inputFormat)
        {
            manifest.InputFormat = inputFormat;
        }

        if (ScriptIo.TryParse(extension?.Output) is { } outputFormat)
        {
            manifest.OutputFormat = outputFormat;
        }

        ApplyParameters(manifest, descriptor.InputSchema);
        ApplyFileInput(manifest, descriptor, extension);

        if (extension?.Function is { Length: > 0 } function)
        {
            manifest.EntryFunction = function.Trim();
        }

        return manifest;
    }

    /// <summary>
    /// Turns each scalar <c>inputSchema</c> property into a typed parameter. Primary file-input
    /// properties (those carrying a <c>contentMediaType</c> without <c>format: file-path</c>) are
    /// skipped here and handled by <see cref="ApplyFileInput"/>.
    /// </summary>
    private static void ApplyParameters(PowerScriptManifest manifest, JsonSchemaObject? inputSchema)
    {
        if (inputSchema?.Properties is not { } properties)
        {
            return;
        }

        foreach (var (name, schema) in properties)
        {
            if (schema.IsPrimaryFileInput)
            {
                continue;
            }

            manifest.Parameters.Add(new ScriptParameter
            {
                Name = name,
                Type = MapType(schema),
                Description = schema.Description,
                Default = schema.DefaultAsString(),
                IsRequired = inputSchema.Required?.Contains(name, StringComparer.Ordinal) == true,
                Options = schema.Enum is { Count: > 0 } options ? new List<string>(options) : new List<string>(),
                Min = schema.Minimum,
                Max = schema.Maximum,
            });
        }
    }

    /// <summary>Maps a JSON Schema property to one of the manifest's parameter types.</summary>
    private static string MapType(JsonSchemaProperty schema)
    {
        if (schema.IsFileParameter)
        {
            return ScriptParameter.ParameterTypeFile;
        }

        if (schema.Enum is { Count: > 0 })
        {
            return ScriptParameter.ParameterTypeChoice;
        }

        return (schema.Type ?? string.Empty).ToLowerInvariant() switch
        {
            "integer" or "number" => ScriptParameter.ParameterTypeInt,
            "boolean" => ScriptParameter.ParameterTypeBool,
            _ => ScriptParameter.ParameterTypeString,
        };
    }

    /// <summary>
    /// Populates the file input contract when the script is file-driven, so the registry can offer it
    /// in the Explorer right-click menu. A script is file-driven when it declares
    /// <c>x-powerscript.extensions</c>, has a file-typed input property, or explicitly declares
    /// <c>input: files</c>. There is no separate "kind" — a non-file script simply has no
    /// <see cref="PowerScriptManifest.Input"/>. File extensions come from the declaration (default
    /// <c>*</c> = any file).
    /// </summary>
    private static void ApplyFileInput(
        PowerScriptManifest manifest,
        ToolDescriptor descriptor,
        PowerScriptExtension? extension)
    {
        var hasFileInput = descriptor.InputSchema?.Properties?.Values.Any(p => p.IsPrimaryFileInput) ?? false;
        var hasExtensions = extension?.Extensions is { Count: > 0 };
        var declaresFiles = manifest.InputFormat == PowerScriptDataFormat.Files;

        if (!hasFileInput && !hasExtensions && !declaresFiles)
        {
            return;
        }

        manifest.Input = new ScriptInput
        {
            Extensions = extension?.Extensions is { Count: > 0 } exts ? new List<string>(exts) : new List<string> { "*" },
            MinFiles = extension?.MinFiles ?? 1,
            MaxFiles = extension?.MaxFiles ?? 0,
        };
    }

    private static ScriptRuntime ResolveRuntime(string? declared, string descriptorPath, string entry)
    {
        if (!string.IsNullOrWhiteSpace(declared))
        {
            if (declared.Equals("python", StringComparison.OrdinalIgnoreCase))
            {
                return ScriptRuntime.Python;
            }

            if (declared.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                declared.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
            {
                return ScriptRuntime.PowerShell;
            }
        }

        // Infer from the entry's extension (.py => Python, else PowerShell) using the same rule as the
        // header path, keying off the sibling script name rather than the ".json" descriptor.
        return ScriptHeaderParser.InferRuntime(entry);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
