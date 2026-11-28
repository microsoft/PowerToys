// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>Whether Text Expander is on, and if not, who turned it off.</summary>
internal enum TextExpanderAvailability
{
    On,

    /// <summary>Off in PowerToys Settings, or never turned on: Text Expander is off by default.</summary>
    Off,

    /// <summary>Off by group policy, which the user can't change.</summary>
    OffByPolicy,
}

/// <summary>Decides whether Text Expander is on, as the runner does.</summary>
internal static class TextExpanderModuleState
{
    /// <summary>
    /// A policy that turns Text Expander on or off wins. Otherwise, PowerToys' setting decides, and a missing one means
    /// off, the module's default.
    /// </summary>
    public static TextExpanderAvailability Evaluate(PolicyRule policy, bool? enabledInSettings) => policy switch
    {
        PolicyRule.Disabled => TextExpanderAvailability.OffByPolicy,
        PolicyRule.Enabled => TextExpanderAvailability.On,
        _ => enabledInSettings == true ? TextExpanderAvailability.On : TextExpanderAvailability.Off,
    };

    /// <summary>
    /// Reads Text Expander's switch from PowerToys' general settings (<c>"enabled": { "TextExpander": true }</c>).
    /// Returns false when the switch is missing, and null when <paramref name="json"/> isn't settings at all, such as a
    /// file caught mid-save, so the caller can keep what it read before.
    /// </summary>
    public static bool? ReadEnabledFlag(string json)
    {
        // Older writers padded the file with null characters.
        json = json.Trim('\0', ' ', '\r', '\n', '\t');
        if (json.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!document.RootElement.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return enabled.TryGetProperty(TextExpanderPaths.ModuleKey, out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
