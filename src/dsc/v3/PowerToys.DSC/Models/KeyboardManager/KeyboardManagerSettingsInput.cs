// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Keyboard Manager's settings.json also holds state that the engine and the
/// editor own: which profile is active ("activeConfiguration") and the list of
/// profiles ("keyboardConfigurations"). The settings resource replaces the
/// whole settings object with the desired state, so an input that leaves
/// these two out would reset them to the model's defaults ("default" and
/// ["default"]).
/// </summary>
public static class KeyboardManagerSettingsInput
{
    /// <summary>
    /// Copies the active profile and the profile list from
    /// <paramref name="current"/> into <paramref name="desired"/> for each of
    /// the two that <paramref name="input"/> (the raw input JSON of the
    /// settings resource) leaves out or sets to null. A value given in the
    /// input is kept as given.
    /// </summary>
    public static void KeepProfileFieldsWhenOmitted(JsonNode? input, KeyboardManagerSettings desired, KeyboardManagerSettings current)
    {
        if (desired?.Properties == null || current?.Properties == null)
        {
            return;
        }

        var properties = (input as JsonObject)?[SettingsResourceObject<KeyboardManagerSettings>.SettingsJsonPropertyName]?["properties"] as JsonObject;

        if (properties?["activeConfiguration"] is null && current.Properties.ActiveConfiguration != null)
        {
            desired.Properties.ActiveConfiguration = current.Properties.ActiveConfiguration;
        }

        if (properties?["keyboardConfigurations"] is null && current.Properties.KeyboardConfigurations != null)
        {
            desired.Properties.KeyboardConfigurations = current.Properties.KeyboardConfigurations;
        }
    }
}
