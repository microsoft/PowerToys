// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Conversions between a string list and the JSON array used by both the settings file and the
/// adaptive card.
/// </summary>
/// <remarks>
/// Entries are objects — <c>[{ "value": "…" }]</c> — so per-item properties can be added later
/// without changing the shape. Readers also accept an array of bare strings, and ignore
/// properties they do not recognize.
/// </remarks>
internal static class StringListSettingUtilities
{
    private const string ValuePropertyName = "value";

    /// <summary>
    /// Writes the entries, for either the settings file or a custom list input.
    /// </summary>
    public static string ToJsonArray(IEnumerable<string>? items) =>
        BuildArray(items).ToJsonString();

    /// <summary>
    /// Writes the entries as this setting's line in the settings file.
    /// </summary>
    public static string ToPersistedValue(string key, IEnumerable<string>? items) =>
        $"\"{key}\": {ToJsonArray(items)}";

    /// <summary>
    /// Reads entries from a parsed array, ignoring anything that is not a string or an object
    /// carrying a string <c>value</c>.
    /// </summary>
    public static IReadOnlyList<string> ParseArray(JsonArray array)
    {
        var items = new List<string>(array.Count);
        foreach (var entry in array)
        {
            var text = entry is JsonObject entryObject ? AsString(entryObject[ValuePropertyName]) : AsString(entry);
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(text);
            }
        }

        return items;
    }

    /// <summary>
    /// Reads entries from unparsed text. Returns <see langword="false"/> when the text is not a
    /// well-formed array, so the caller can keep the value it already has.
    /// </summary>
    public static bool TryParseJsonArray(string? value, out IReadOnlyList<string> items)
    {
        items = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is not JsonArray array)
        {
            return false;
        }

        items = ParseArray(array);
        return true;
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string?>? items) =>
        items?
            .Where(static item => !string.IsNullOrEmpty(item))
            .Select(static item => item!)
            .ToArray() ?? [];

    private static JsonArray BuildArray(IEnumerable<string>? items) =>
        new(Normalize(items)
            .Select(static item => (JsonNode)new JsonObject { [ValuePropertyName] = item })
            .ToArray());

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
