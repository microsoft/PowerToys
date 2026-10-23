// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

public sealed record ScriptTemplate(string Id, string Name, string Description, bool AcceptsFiles = false);

public sealed record ScriptTemplateOptions(string ProjectName, string? Description = null, string? FileExtensions = null);

public sealed record CreatedScriptTemplate(string EntryPath, string GuidePath);

/// <summary>Creates editable packages, never trust approvals or policy exceptions.</summary>
public static class ScriptTemplates
{
    public static IReadOnlyList<ScriptTemplate> All { get; } = Array.AsReadOnly(new[]
    {
        new ScriptTemplate("greeting", "Greeting with parameters", "No primary input → text. Learn identity, typed parameters, defaults and discovery. Runs with all MXC restrictions enabled."),
        new ScriptTemplate("report", "Create a text file", "No primary input → files. Learn file output, UTF-8 and fileWrite capabilities. Writes only to the isolated temporary workspace; no policy relaxation."),
        new ScriptTemplate("file-info", "Describe selected files", "Files → text. Choose which file extensions to accept and return each selected file's name and size. No policy relaxation.", AcceptsFiles: true),
    });

    public static CreatedScriptTemplate Create(string id, string scriptsRoot, ScriptTemplateOptions? options = null)
    {
        var template = All.SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new ArgumentException("Unknown script template.", nameof(id));
        var descriptor = JsonNode.Parse(ReadAsset(template.Id, "script.ps1.tool.json"))!.AsObject();
        var projectName = (options is null ? template.Name : options.ProjectName)?.Trim();
        if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > 80 || projectName.Any(char.IsControl))
        {
            throw new ArgumentException("Enter a project name of 1 to 80 characters without control characters.", nameof(options));
        }

        var description = options?.Description ?? descriptor["description"]!.GetValue<string>();
        if (description.Length > 500 || description.Contains('\0'))
        {
            throw new ArgumentException("The description must be at most 500 characters and cannot contain null characters.", nameof(options));
        }

        var extension = descriptor["x-powerscript"]!.AsObject();
        if (template.AcceptsFiles)
        {
            extension["extensions"] = JsonSerializer.SerializeToNode(ParseFileExtensions(options?.FileExtensions));
        }
        else if (!string.IsNullOrWhiteSpace(options?.FileExtensions))
        {
            throw new ArgumentException("This template does not accept file inputs, so it cannot have an extension filter.", nameof(options));
        }

        var slug = new string(projectName.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        var packageName = (slug.Length == 0 ? template.Id : slug) + "-" + Guid.NewGuid().ToString("N");
        var folder = Path.Combine(Path.GetFullPath(scriptsRoot), packageName);
        descriptor["name"] = packageName;
        descriptor["title"] = projectName;
        descriptor["description"] = description;
        var manifest = ToolDescriptorParser.Parse(descriptor.Deserialize<ToolDescriptor>(), "script.ps1")
            ?? throw new InvalidDataException("The template descriptor is invalid.");
        var errors = ManifestValidator.Validate(manifest, packageName);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        Directory.CreateDirectory(folder);
        var entry = Path.Combine(folder, "script.ps1");
        var guide = Path.Combine(folder, "README.md");
        WriteNew(entry, ReadAsset(template.Id, "script.ps1"));
        WriteNew(guide, ReadAsset(template.Id, "README.md"));
        // Publish the descriptor last so incomplete packages are not discovered as runnable scripts.
        WriteNew(entry + ToolDescriptorParser.DescriptorSuffix, descriptor.ToJsonString(new() { WriteIndented = true }));
        return new CreatedScriptTemplate(entry, guide);
    }

    private static IReadOnlyList<string> ParseFileExtensions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { "*" };
        }

        if (text.Length > 1024)
        {
            throw new ArgumentException("The file extension list is too long.", nameof(text));
        }

        var extensions = text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("*.", StringComparison.Ordinal) ? value[1..] : value)
            .Select(value => value == "*" || value.StartsWith('.') ? value : "." + value)
            .Select(value => value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (extensions.Length is 0 or > 32 ||
            (extensions.Contains("*") && extensions.Length != 1) ||
            extensions.Any(value => value != "*" &&
                (value.Length is < 2 or > 32 || value[1..].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))))
        {
            throw new ArgumentException("Use extensions such as .txt, .md or *.png, separated by commas, spaces or semicolons. Leave blank for all types, or use * alone. Use the final extension for compound filenames (for example .gz).", nameof(text));
        }

        return extensions;
    }

    private static string ReadAsset(string id, string file)
    {
        // MSBuild replaces hyphens in resource directory names with underscores.
        var resourceId = id.Replace('-', '_');
        using var stream = typeof(ScriptTemplates).Assembly.GetManifestResourceStream($"PowerScripts.Core.Templates.{resourceId}.{file}")
            ?? throw new InvalidDataException($"Missing script template asset: {id}/{file}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteNew(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
