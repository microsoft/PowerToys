// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class LightSwitchProfileSettingsUpdater
    {
        public static bool ClearDeletedProfileAndSend(
            LightSwitchSettings settings,
            Guid deletedProfileId,
            Func<string, int> sendConfigMessage,
            int? legacyId = null,
            PowerDisplayProfiles? profilesBeforeDeletion = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(sendConfigMessage);

            if (deletedProfileId == Guid.Empty)
            {
                throw new ArgumentException("A deleted profile must have a UUID.", nameof(deletedProfileId));
            }

            // Resolve name-only references before the profile disappears. Otherwise a new
            // same-name profile could inherit a reference intended for the deleted profile.
            var changed = profilesBeforeDeletion is not null &&
                LightSwitchProfileReferenceHelper.ReconcileReferences(settings.Properties, profilesBeforeDeletion);
            changed |= LightSwitchProfileReferenceHelper.ClearProfileIdReferences(
                settings.Properties,
                deletedProfileId,
                legacyId);
            if (!changed)
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
