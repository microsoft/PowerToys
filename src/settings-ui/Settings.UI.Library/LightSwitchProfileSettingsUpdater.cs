// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class LightSwitchProfileSettingsUpdater
    {
        public static bool ClearDeletedProfileAndSend(
            LightSwitchSettings settings,
            int deletedProfileId,
            Func<string, int> sendConfigMessage)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(sendConfigMessage);

            if (!LightSwitchProfileReferenceHelper.ClearProfileIdReferences(
                settings.Properties,
                deletedProfileId))
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
