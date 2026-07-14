// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class LightSwitchProfileSettingsUpdater
    {
        public static bool ReconcileAndSend(
            LightSwitchSettings settings,
            PowerDisplayProfiles profiles,
            Func<string, int> sendConfigMessage)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(sendConfigMessage);

            if (!LightSwitchProfileReferenceHelper.ReconcileReferences(settings.Properties, profiles))
            {
                return false;
            }

            var outgoing = new SndModuleSettings<SndLightSwitchSettings>(
                new SndLightSwitchSettings(settings));
            sendConfigMessage(outgoing.ToJsonString());
            return true;
        }
    }
}
