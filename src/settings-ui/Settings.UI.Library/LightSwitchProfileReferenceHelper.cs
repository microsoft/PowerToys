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

        public static Guid? GetProfileIdForTheme(LightSwitchProperties properties, bool isLightMode)
        {
            ArgumentNullException.ThrowIfNull(properties);

            var enabled = isLightMode
                ? properties.EnableLightModeProfile.Value
                : properties.EnableDarkModeProfile.Value;
            var profileId = isLightMode
                ? properties.LightModeProfileId.Value
                : properties.DarkModeProfileId.Value;

            return enabled && profileId != Guid.Empty ? profileId : null;
        }

        public static bool SetProfileId(
            ProfileIdProperty idProperty,
            StringProperty legacyNameProperty,
            Guid profileId)
        {
            ArgumentNullException.ThrowIfNull(idProperty);
            ArgumentNullException.ThrowIfNull(legacyNameProperty);

            if (idProperty.Value == profileId
                && idProperty.LegacyId is null
                && string.IsNullOrEmpty(legacyNameProperty.Value))
            {
                return false;
            }

            idProperty.Value = profileId;
            idProperty.LegacyId = null;
            legacyNameProperty.Value = string.Empty;
            return true;
        }

        public static bool ClearProfileIdReferences(
            LightSwitchProperties properties,
            Guid profileId,
            int? legacyId = null)
        {
            ArgumentNullException.ThrowIfNull(properties);

            if (profileId == Guid.Empty)
            {
                throw new ArgumentException("A deleted profile must have a UUID.", nameof(profileId));
            }

            var changed = ClearOne(properties.LightModeProfileId, properties.LightModeProfile, profileId, legacyId);
            changed |= ClearOne(properties.DarkModeProfileId, properties.DarkModeProfile, profileId, legacyId);
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

        private static bool ClearOne(ProfileIdProperty reference, StringProperty legacyName, Guid profileId, int? legacyId)
        {
            if (reference.Value != profileId &&
                !(reference.Value == Guid.Empty && legacyId is > 0 && reference.LegacyId == legacyId))
            {
                return false;
            }

            // Clearing a deleted reference also clears its migration fallback, so a later
            // same-name profile cannot accidentally restore the deleted selection.
            return SetProfileId(reference, legacyName, Guid.Empty);
        }

        private static bool ReconcileOne(
            PowerDisplayProfiles profiles,
            ProfileIdProperty idProperty,
            StringProperty legacyNameProperty)
        {
            var originalId = idProperty.Value;
            if (originalId != Guid.Empty)
            {
                // A canonical UUID takes precedence over all older references. A missing
                // profile can be temporary, so only an explicit user action clears it.
                return profiles.GetById(originalId) is not null &&
                    SetProfileId(idProperty, legacyNameProperty, originalId);
            }

            PowerDisplayProfile? profile = null;
            if (idProperty.LegacyId is > 0)
            {
                profile = profiles.GetByLegacyId(idProperty.LegacyId.Value);
            }
            else if (!string.IsNullOrEmpty(legacyNameProperty.Value) && legacyNameProperty.Value != NoneSentinel)
            {
                profile = profiles.GetLegacyProfileByName(legacyNameProperty.Value);
            }

            // Profiles must be the committed snapshot returned by ProfileStore. If either
            // file failed to migrate, retain the old reference for the next attempt.
            return profile is not null && profile.Id != Guid.Empty &&
                SetProfileId(idProperty, legacyNameProperty, profile.Id);
        }
    }
}
