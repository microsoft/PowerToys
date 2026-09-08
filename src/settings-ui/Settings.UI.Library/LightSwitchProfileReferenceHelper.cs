// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class LightSwitchProfileReferenceHelper
    {
        public const string NoneSentinel = "(None)";

        public static int? GetProfileIdForTheme(LightSwitchProperties properties, bool isLightMode)
        {
            ArgumentNullException.ThrowIfNull(properties);

            var enabled = isLightMode
                ? properties.EnableLightModeProfile.Value
                : properties.EnableDarkModeProfile.Value;
            var profileId = isLightMode
                ? properties.LightModeProfileId.Value
                : properties.DarkModeProfileId.Value;

            return enabled && profileId >= 1 ? profileId : null;
        }

        public static bool SetProfileId(
            IntProperty idProperty,
            StringProperty legacyNameProperty,
            int profileId)
        {
            ArgumentNullException.ThrowIfNull(idProperty);
            ArgumentNullException.ThrowIfNull(legacyNameProperty);

            ArgumentOutOfRangeException.ThrowIfNegative(profileId);

            if (idProperty.Value == profileId
                && string.IsNullOrEmpty(legacyNameProperty.Value))
            {
                return false;
            }

            idProperty.Value = profileId;
            legacyNameProperty.Value = string.Empty;
            return true;
        }

        public static bool ClearProfileIdReferences(
            LightSwitchProperties properties,
            int profileId)
        {
            ArgumentNullException.ThrowIfNull(properties);

            ArgumentOutOfRangeException.ThrowIfLessThan(profileId, 1);

            var changed = false;
            if (properties.LightModeProfileId.Value == profileId)
            {
                properties.LightModeProfileId.Value = 0;
                changed = true;
            }

            if (properties.DarkModeProfileId.Value == profileId)
            {
                properties.DarkModeProfileId.Value = 0;
                changed = true;
            }

            return changed;
        }

        public static bool ReconcileReferences(
            LightSwitchProperties properties,
            PowerDisplayProfiles profiles)
        {
            ArgumentNullException.ThrowIfNull(properties);
            ArgumentNullException.ThrowIfNull(profiles);

            var changed = false;
            changed |= ReconcileOne(
                profiles,
                properties.LightModeProfileId,
                properties.LightModeProfile);
            changed |= ReconcileOne(
                profiles,
                properties.DarkModeProfileId,
                properties.DarkModeProfile);
            return changed;
        }

        private static bool ReconcileOne(
            PowerDisplayProfiles profiles,
            IntProperty idProperty,
            StringProperty legacyNameProperty)
        {
            var originalId = idProperty.Value;
            var originalName = legacyNameProperty.Value;

            if (originalId >= 1)
            {
                if (profiles.GetById(originalId) is null)
                {
                    idProperty.Value = 0;
                }
            }
            else if (!string.IsNullOrEmpty(originalName) && originalName != NoneSentinel)
            {
                var profile = profiles.GetLegacyProfileByName(originalName);
                if (profile is not null && profile.Id >= 1)
                {
                    idProperty.Value = profile.Id;
                }
            }

            if (!string.IsNullOrEmpty(legacyNameProperty.Value))
            {
                legacyNameProperty.Value = string.Empty;
            }

            return idProperty.Value != originalId
                || legacyNameProperty.Value != originalName;
        }
    }
}
