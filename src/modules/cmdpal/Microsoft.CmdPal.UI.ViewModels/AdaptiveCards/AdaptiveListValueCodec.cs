// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

#pragma warning disable SA1402 // File may only contain a single type

namespace Microsoft.CmdPal.UI.ViewModels.AdaptiveCards;

/// <summary>
/// Reads and writes the <c>value</c> of Command Palette's custom list inputs.
/// </summary>
/// <remarks>
/// This is the host's half of a wire contract shared with
/// <c>Microsoft.CommandPalette.Extensions.Toolkit</c>: a JSON array of objects, so per-item
/// properties can be added later without changing the shape. An array of bare strings is also
/// accepted. Properties that are not recognized are preserved rather than dropped, so an extension
/// built against a newer SDK does not lose per-item state when the form is saved here.
/// </remarks>
public static class AdaptiveListValueCodec
{
    public const string KeyPropertyName = "key";
    public const string ValuePropertyName = "value";

    /// <summary>
    /// Reads list entries. Returns <see langword="false"/> when the value is present but is not a
    /// well-formed array, so the caller can avoid replacing a value it could not read.
    /// </summary>
    public static bool TryParseItems(string? value, out List<AdaptiveListItemValue> items)
    {
        items = [];
        if (!TryParseArray(value, out var array))
        {
            return false;
        }

        foreach (var entry in array)
        {
            var source = entry as JsonObject;
            var text = source is null ? AsString(entry) : AsString(source[ValuePropertyName]);
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(new AdaptiveListItemValue(text, source));
            }
        }

        return true;
    }

    public static string ToItemsValue(IEnumerable<AdaptiveListItemValue> items) =>
        new JsonArray(items
            .Select(static item => (JsonNode)item.ToJson((ValuePropertyName, item.Value)))
            .ToArray())
            .ToJsonString();

    /// <summary>
    /// Reads key/value entries. Returns <see langword="false"/> when the value is present but is
    /// not a well-formed array.
    /// </summary>
    public static bool TryParsePairs(string? value, out List<AdaptiveKeyValuePairValue> pairs)
    {
        pairs = [];
        if (!TryParseArray(value, out var array))
        {
            return false;
        }

        foreach (var entry in array)
        {
            if (entry is not JsonObject source)
            {
                continue;
            }

            pairs.Add(new AdaptiveKeyValuePairValue(
                AsString(source[KeyPropertyName]) ?? string.Empty,
                AsString(source[ValuePropertyName]) ?? string.Empty,
                source));
        }

        return true;
    }

    public static string ToPairsValue(IEnumerable<AdaptiveKeyValuePairValue> pairs) =>
        new JsonArray(pairs
            .Select(static pair => (JsonNode)pair.ToJson(
                (KeyPropertyName, pair.Key),
                (ValuePropertyName, pair.Value)))
            .ToArray())
            .ToJsonString();

    private static bool TryParseArray(string? value, out JsonArray array)
    {
        array = [];
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

        if (node is not JsonArray parsed)
        {
            return false;
        }

        array = parsed;
        return true;
    }

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>
/// One entry of a custom list input, along with the JSON object it was parsed from.
/// </summary>
public abstract class AdaptiveListEntryValue(JsonObject? source)
{
    /// <summary>
    /// Gets the object this entry was parsed from, or <see langword="null"/> for an entry the user
    /// just added.
    /// </summary>
    protected JsonObject? Source { get; } = source;

    /// <summary>
    /// Returns the source object with the supplied properties applied, so properties this version
    /// does not recognize are carried through unchanged.
    /// </summary>
    internal JsonObject ToJson(params (string Name, string Value)[] properties)
    {
        var json = Source?.DeepClone().AsObject() ?? [];
        foreach (var (name, value) in properties)
        {
            json[name] = value;
        }

        return json;
    }
}

/// <summary>
/// One entry of a string or path list.
/// </summary>
public sealed class AdaptiveListItemValue(string value, JsonObject? source = null)
    : AdaptiveListEntryValue(source)
{
    public string Value { get; } = value;
}

/// <summary>
/// One entry of a key/value list.
/// </summary>
public sealed class AdaptiveKeyValuePairValue(string key, string value, JsonObject? source = null)
    : AdaptiveListEntryValue(source)
{
    public string Key { get; } = key;

    public string Value { get; } = value;
}

#pragma warning restore SA1402 // File may only contain a single type
