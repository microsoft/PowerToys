// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;

namespace PowerDisplay.Services
{
    /// <summary>
    /// Pure resolution + migration helpers for the LightSwitch theme->profile mapping. The mapping
    /// stores both a canonical profile id and a display/fallback name; resolution prefers the id.
    /// </summary>
    public static class LightSwitchProfileResolver
    {
        /// <summary>None sentinel that older/manual settings may hold in the name field.</summary>
        public const string NoneSentinel = "(None)";

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
        /// One-shot upgrade of a LightSwitch mapping from name-only to id+name. For each of light and
        /// dark: when the id is unset (0) and the stored name resolves to a profile, store its id and
        /// re-sync the name; when the name does not resolve, clear to "none" (id 0, empty name).
        /// Returns true when anything changed. Idempotent.
        /// </summary>
        public static bool MigrateNamesToIds(LightSwitchProperties props, PowerDisplayProfiles profiles)
        {
            if (props is null || profiles is null)
            {
                return false;
            }

            var changed = false;
            changed |= MigrateOne(profiles, props.LightModeProfileId, props.LightModeProfile);
            changed |= MigrateOne(profiles, props.DarkModeProfileId, props.DarkModeProfile);
            return changed;
        }

        private static bool MigrateOne(PowerDisplayProfiles profiles, IntProperty idProp, StringProperty nameProp)
        {
            if (idProp.Value >= 1)
            {
                return false; // already migrated
            }

            var name = nameProp.Value;
            if (string.IsNullOrEmpty(name) || name == NoneSentinel)
            {
                return false; // already "none"
            }

            var match = profiles.GetProfile(name);
            if (match is not null && match.Id >= 1)
            {
                idProp.Value = match.Id;
                nameProp.Value = match.Name;
            }
            else
            {
                nameProp.Value = string.Empty; // unknown/deleted -> none
            }

            return true;
        }
    }
}
