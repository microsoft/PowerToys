// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Pure resolution + migration helpers for the LightSwitch theme->profile mapping. The mapping
    /// stores both a canonical profile id and a display/fallback name; resolution prefers the id.
    /// Shared by the PowerDisplay app (theme handling) and the Settings UI (profile selection).
    /// </summary>
    public static class LightSwitchProfileResolver
    {
        /// <summary>None sentinel that older/manual settings may hold in the name field.</summary>
        public const string NoneSentinel = "(None)";

        /// <summary>
        /// Returns true when the stored (id, name) pair points at a profile (i.e. is not "none").
        /// Used to tell an unresolved-but-set reference (stale, should be cleared) apart from an
        /// intentionally empty selection.
        /// </summary>
        public static bool HasReference(int storedId, string? storedName)
        {
            return storedId >= 1 || (!string.IsNullOrEmpty(storedName) && storedName != NoneSentinel);
        }

        /// <summary>
        /// Returns the effective profile id for a stored (id, name) pair, or null for "none":
        /// a positive id that still exists wins; otherwise a non-empty, non-"(None)" name resolves
        /// by first match; otherwise null. A stale/deleted id does NOT fall back to the name.
        /// </summary>
        public static int? Resolve(int storedId, string? storedName, PowerDisplayProfiles profiles)
        {
            if (profiles is null)
            {
                return null;
            }

            if (storedId >= 1)
            {
                return profiles.GetById(storedId) is not null ? storedId : (int?)null;
            }

            if (!string.IsNullOrEmpty(storedName) && storedName != NoneSentinel)
            {
                var byName = profiles.GetProfile(storedName);
                if (byName is not null && byName.Id >= 1)
                {
                    return byName.Id;
                }
            }

            return null;
        }

        /// <summary>
        /// Reconciles the stored light/dark profile references. When an id is set, a missing profile
        /// clears both the id and name. When the id is unset, a resolvable name upgrades to id+name and
        /// an unknown name clears to "none" (empty name, id 0). Returns true when anything changed.
        /// </summary>
        public static bool ReconcileReferences(LightSwitchProperties props, PowerDisplayProfiles profiles)
        {
            ArgumentNullException.ThrowIfNull(props);
            ArgumentNullException.ThrowIfNull(profiles);

            var changed = false;
            changed |= ReconcileOne(profiles, props.LightModeProfileId, props.LightModeProfile);
            changed |= ReconcileOne(profiles, props.DarkModeProfileId, props.DarkModeProfile);
            return changed;
        }

        /// <summary>
        /// Compatibility alias kept for stacked consumers while they move to <see cref="ReconcileReferences"/>.
        /// </summary>
        public static bool MigrateNamesToIds(LightSwitchProperties props, PowerDisplayProfiles profiles)
            => ReconcileReferences(props, profiles);

        private static bool ReconcileOne(PowerDisplayProfiles profiles, IntProperty idProp, StringProperty nameProp)
        {
            if (idProp.Value >= 1)
            {
                if (profiles.GetById(idProp.Value) is not null)
                {
                    return false;
                }

                idProp.Value = 0;
                nameProp.Value = string.Empty;
                return true;
            }

            var name = nameProp.Value;
            if (string.IsNullOrEmpty(name) || name == NoneSentinel)
            {
                return false;
            }

            var match = profiles.GetProfile(name);
            if (match is not null && match.Id >= 1)
            {
                idProp.Value = match.Id;
                nameProp.Value = match.Name;
            }
            else
            {
                nameProp.Value = string.Empty;
            }

            return true;
        }
    }
}
