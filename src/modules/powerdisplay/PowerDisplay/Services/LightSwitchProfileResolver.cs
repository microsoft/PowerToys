// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    }
}
