// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// The Text Expander settings this extension follows, from <c>TextExpander\settings.json</c>: where the snippets
/// file is, whether triggers match capitalization, and whether snippets must be typed rather than pasted.
/// </summary>
internal sealed record TextExpanderSettings(string? SnippetsPath, bool CaseSensitive, bool AlwaysType)
{
    /// <summary>Gets Text Expander's defaults: the default snippets file, capitalization matters, paste when it can.</summary>
    public static TextExpanderSettings Default { get; } = new(SnippetsPath: null, CaseSensitive: true, AlwaysType: false);

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

            var backend = ReadString(properties, "injection_backend");
            return new TextExpanderSettings(
                ReadString(properties, "snippets_path"),
                ReadBool(properties, "case_sensitive") ?? Default.CaseSensitive,
                IsTypingBackend(backend));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The names the engine accepts for its typing backend (<c>InjectionPolicy.ParseBackend</c>).</summary>
    private static bool IsTypingBackend(string? backend) => backend?.ToLowerInvariant() switch
    {
        "type" or "typing" or "inject" or "keyboard" => true,
        _ => false,
    };

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
}
