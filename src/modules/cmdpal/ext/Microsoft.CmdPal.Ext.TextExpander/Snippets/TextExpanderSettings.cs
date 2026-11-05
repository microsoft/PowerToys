// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// The Text Expander settings this extension follows, from <c>TextExpander\settings.json</c>: where the snippets
/// file is, whether triggers match capitalization, and the insertion method, with the shortest snippet Automatic pastes.
/// </summary>
internal sealed record TextExpanderSettings(string? SnippetsPath, bool CaseSensitive, InjectionBackend Backend, int ClipboardThresholdChars)
{
    /// <summary>Gets Text Expander's defaults: the default snippets file, capitalization matters, Automatic.</summary>
    public static TextExpanderSettings Default { get; } =
        new(SnippetsPath: null, CaseSensitive: true, InjectionBackend.Auto, InjectionPolicy.DefaultClipboardThresholdChars);

    /// <summary>
    /// Parses the settings as the engine does (<c>HostSettings.Parse</c>): a <c>properties</c> object whose values are
    /// bare or wrapped as <c>{"value": x}</c>. A missing value keeps the default. Returns null when
    /// <paramref name="json"/> isn't settings, such as a file caught mid-save, so the caller can keep what it read before.
    /// </summary>
    public static TextExpanderSettings? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("properties", out var properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new TextExpanderSettings(
                ReadString(properties, "snippets_path"),
                ReadBool(properties, "case_sensitive") ?? Default.CaseSensitive,
                InjectionPolicy.ParseBackend(ReadString(properties, "injection_backend"), Default.Backend),
                ReadInt(properties, "clipboard_threshold_chars") is int threshold && threshold >= 1 ? threshold : Default.ClipboardThresholdChars);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetValue(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        if (!parent.TryGetProperty(name, out var element))
        {
            return false;
        }

        value = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out var inner) ? inner : element;
        return true;
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool? ReadBool(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    // A number, or a string of one, as the engine takes it from a file edited by hand.
    private static int? ReadInt(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
