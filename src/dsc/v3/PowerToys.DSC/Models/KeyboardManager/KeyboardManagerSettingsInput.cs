// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Keyboard Manager's settings.json also holds state that the engine and the
/// editor own: which profile is active ("activeConfiguration") and the list of
/// profiles ("keyboardConfigurations"). They change while PowerToys runs
/// (device auto-switch, the cycle hotkey, creating or deleting a profile in
/// the editor) and each name stands for a "{name}.json" file next to the
/// settings. The settings resource replaces the whole settings object with
/// the desired state, so without this an input would reset the two to the
/// model's defaults when it leaves them out, and when it has them (an export
/// from another machine, or an older export from this one) it would select a
/// profile whose file may not exist, which leaves the engine with no
/// remappings. The settings resource therefore does not manage these two:
/// the desired state always takes them from the current state.
/// </summary>
public static class KeyboardManagerSettingsInput
{
    private static readonly CompositeFormat ProfileFieldNotApplied = CompositeFormat.Parse(Resources.KeyboardManagerProfileFieldNotApplied);

    /// <summary>
    /// The warning for a profile field of the input that is not applied.
    /// </summary>
    /// <param name="field">The name of the settings property.</param>
    public static string FormatProfileFieldNotApplied(string field) =>
        string.Format(CultureInfo.InvariantCulture, ProfileFieldNotApplied, field);

    /// <summary>
    /// Copies the active profile and the profile list from
    /// <paramref name="current"/> into <paramref name="desired"/>, whatever
    /// the input said about them.
    /// </summary>
    public static void UseCurrentProfileFields(KeyboardManagerSettings desired, KeyboardManagerSettings current)
    {
        if (desired?.Properties == null || current?.Properties == null)
        {
            return;
        }

        desired.Properties.ActiveConfiguration = current.Properties.ActiveConfiguration;
        desired.Properties.KeyboardConfigurations = current.Properties.KeyboardConfigurations;
    }

    /// <summary>
    /// The names of the profile fields that the input gives with a value other
    /// than the current one. <see cref="UseCurrentProfileFields"/> does not
    /// apply them, and the caller is told so with a warning instead of
    /// silently reporting the input as applied.
    /// </summary>
    /// <param name="input">The JSON input of the settings resource.</param>
    /// <param name="current">The current settings.</param>
    public static IReadOnlyList<string> GetProfileFieldsNotApplied(string? input, KeyboardManagerSettings current)
    {
        var notApplied = new List<string>();
        if (string.IsNullOrEmpty(input) || current?.Properties == null)
        {
            return notApplied;
        }

        try
        {
            JsonNode? properties = JsonNode.Parse(input)?["settings"]?["properties"];

            if (properties?["activeConfiguration"]?["value"] is JsonValue active &&
                active.TryGetValue(out string? activeName) &&
                !string.Equals(activeName, current.Properties.ActiveConfiguration?.Value, StringComparison.OrdinalIgnoreCase))
            {
                notApplied.Add("activeConfiguration");
            }

            if (properties?["keyboardConfigurations"]?["value"] is JsonArray list)
            {
                var given = list.Select(item => item is JsonValue value && value.TryGetValue(out string? name) ? name : null);
                IEnumerable<string?> now = current.Properties.KeyboardConfigurations?.Value ?? [];
                if (!given.SequenceEqual(now, StringComparer.OrdinalIgnoreCase))
                {
                    notApplied.Add("keyboardConfigurations");
                }
            }
        }
        catch (Exception)
        {
            // Not the shape this looks for (the typed read of the same input decides whether it
            // is valid): nothing to report here, and a warning must never fail the operation.
            notApplied.Clear();
        }

        return notApplied;
    }
}
