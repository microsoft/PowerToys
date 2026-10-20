// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>
/// The transform contract a Python script exposes (derived from its
/// <c>powerscript_from_&lt;input&gt;_to_&lt;output&gt;</c> function). Non-null only for scripts that can be
/// used as data transforms (e.g. Advanced Paste).
/// </summary>
public sealed record PowerScriptTransformContract(string Function, string InputFormat, string OutputFormat);

/// <summary>A typed parameter contract that a consumer module may render using its own UX.</summary>
public sealed record PowerScriptParameter(
    string Name,
    string Type,
    bool IsRequired,
    string? Label,
    string? Description,
    string? Default,
    IReadOnlyList<string> Options,
    int? Min,
    int? Max);

/// <summary>
/// A single script as reported by <c>PowerScripts.Host.exe list --json</c>. This is the read model
/// every consuming module works from — filter it by I/O (see <see cref="Input"/>/<see cref="Output"/>
/// and the filter methods on <see cref="PowerScriptsClient"/>) to find the scripts relevant to you.
/// </summary>
public sealed class PowerScriptInfo
{
    /// <summary>Stable identity used to run the script (<c>run &lt;id&gt;</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>One-line description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary><c>PowerShell</c> or <c>Python</c>.</summary>
    public string Runtime { get; init; } = string.Empty;

    /// <summary>Optional author/publisher.</summary>
    public string? Publisher { get; init; }

    /// <summary>Optional version string.</summary>
    public string? Version { get; init; }

    /// <summary>Optional provenance (e.g. where the script was adopted from).</summary>
    public string? Source { get; init; }

    /// <summary>The data shape this script consumes. Filter on it to discover usable scripts (e.g. <see cref="PowerScriptIO.Text"/>).</summary>
    public PowerScriptIO Input { get; init; } = PowerScriptIO.None;

    /// <summary>The data shape this script produces.</summary>
    public PowerScriptIO Output { get; init; } = PowerScriptIO.None;

    /// <summary>The capabilities the script declares (e.g. <c>fileRead</c>, <c>network</c>).</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>For <c>file</c> scripts, the file extensions it accepts (e.g. <c>.md</c>); empty otherwise.</summary>
    public IReadOnlyList<string> InputExtensions { get; init; } = [];

    /// <summary>Absolute path to the script's folder (diagnostics only; do not execute directly).</summary>
    public string? FolderPath { get; init; }

    /// <summary>Absolute path to the entry file (diagnostics only; do not execute directly).</summary>
    public string? EntryFullPath { get; init; }

    /// <summary>Whether the user has already approved the script's current content (trust-on-first-use).</summary>
    public bool Trusted { get; init; }

    /// <summary>Typed parameters that the consumer may collect and pass to <c>Run</c>.</summary>
    public IReadOnlyList<PowerScriptParameter> Parameters { get; init; } = [];

    /// <summary>The transform contract, when the script can act as a data transform; otherwise null.</summary>
    public PowerScriptTransformContract? Transform { get; init; }

    /// <summary>True when this script consumes no input (a system action) — e.g. a hotkey or palette command.</summary>
    public bool IsAction => Input == PowerScriptIO.None;

    /// <summary>True when this script consumes <paramref name="input"/>.</summary>
    public bool Accepts(PowerScriptIO input) => Input == input;

    /// <summary>True when this script produces <paramref name="output"/>.</summary>
    public bool Produces(PowerScriptIO output) => Output == output;

    /// <summary>
    /// Parses the JSON array emitted by <c>list --json</c> into typed records. Uses
    /// <see cref="JsonDocument"/> (no reflection) so it is safe under trimming/AOT. Malformed entries
    /// are skipped rather than throwing, matching the host's tolerant surfaces.
    /// </summary>
    public static IReadOnlyList<PowerScriptInfo> ParseList(string json)
    {
        var results = new List<PowerScriptInfo>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return results;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var id = GetString(element, "id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            results.Add(new PowerScriptInfo
            {
                Id = id,
                Name = GetString(element, "name", id),
                Description = GetString(element, "description"),
                Runtime = GetString(element, "runtime"),
                Publisher = GetNullableString(element, "publisher"),
                Version = GetNullableString(element, "version"),
                Source = GetNullableString(element, "source"),
                Input = GetIO(element, "input"),
                Output = GetIO(element, "output"),
                Capabilities = GetStringArray(element, "capabilities"),
                InputExtensions = GetInputExtensions(element),
                FolderPath = GetNullableString(element, "folderPath"),
                EntryFullPath = GetNullableString(element, "entryFullPath"),
                Trusted = GetBool(element, "trusted"),
                Parameters = GetParameters(element),
                Transform = GetTransform(element),
            });
        }

        return results;
    }

    private static string GetString(JsonElement element, string name, string fallback = "") =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static string? GetNullableString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    items.Add(text);
                }
            }
        }

        return items;
    }

    private static PowerScriptIO GetIO(JsonElement element, string name)
    {
        if (element.TryGetProperty("io", out var io) &&
            io.ValueKind == JsonValueKind.Object &&
            io.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return PowerScriptsProtocol.ParseIO(value.GetString());
        }

        return PowerScriptIO.None;
    }

    private static IReadOnlyList<string> GetInputExtensions(JsonElement element)
    {
        if (element.TryGetProperty("input", out var input) &&
            input.ValueKind == JsonValueKind.Object)
        {
            return GetStringArray(input, "extensions");
        }

        return [];
    }

    private static PowerScriptTransformContract? GetTransform(JsonElement element)
    {
        if (!element.TryGetProperty("transform", out var transform) ||
            transform.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PowerScriptTransformContract(
            GetString(transform, "function"),
            GetString(transform, "inputFormat"),
            GetString(transform, "outputFormat"));
    }

    private static IReadOnlyList<PowerScriptParameter> GetParameters(JsonElement element)
    {
        if (!element.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<PowerScriptParameter>();
        foreach (var parameter in parameters.EnumerateArray())
        {
            var name = GetString(parameter, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            results.Add(new PowerScriptParameter(
                name,
                GetString(parameter, "type", "string"),
                GetBool(parameter, "isRequired"),
                GetNullableString(parameter, "label"),
                GetNullableString(parameter, "description"),
                GetNullableString(parameter, "default"),
                GetStringArray(parameter, "options"),
                GetNullableInt(parameter, "min"),
                GetNullableInt(parameter, "max")));
        }

        return results;
    }

    private static int? GetNullableInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : null;
}
