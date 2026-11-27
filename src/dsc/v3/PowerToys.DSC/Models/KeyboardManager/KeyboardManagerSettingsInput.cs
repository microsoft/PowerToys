// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

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
}
