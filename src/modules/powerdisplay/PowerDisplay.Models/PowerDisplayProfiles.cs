// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Container for all PowerDisplay profiles
    /// </summary>
    public class PowerDisplayProfiles
    {
        [JsonPropertyName("profiles")]
        public List<PowerDisplayProfile> Profiles { get; set; }

        [JsonPropertyName("nextId")]
        public int NextId { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        public PowerDisplayProfiles()
        {
            Profiles = new List<PowerDisplayProfile>();
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the first profile whose name matches a pre-ID persisted reference.
        /// This lookup is only for legacy migration because profile names are not unique.
        /// </summary>
        public PowerDisplayProfile? GetLegacyProfileByName(string name)
        {
            return Profiles.FirstOrDefault(
                profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the profile by its stable id, or null when id is not positive or no profile has it.
        /// </summary>
        public PowerDisplayProfile? GetById(int id)
        {
            return id <= 0 ? null : Profiles.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Returns profiles that have a usable stable id.
        /// Legacy or corrupt profiles with non-positive ids remain hidden until migration.
        /// </summary>
        public IEnumerable<PowerDisplayProfile> GetAssignedProfiles()
        {
            return Profiles.Where(profile => profile is not null && profile.Id >= 1);
        }

        /// <summary>
        /// Adds or updates a profile, keyed by its stable id. When the incoming profile has no id
        /// (Id == 0) a new one is assigned from the monotonic NextId counter. Names are not required
        /// to be unique.
        /// </summary>
        public void SetProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                throw new ArgumentException("Profile is invalid");
            }

            if (profile.Id == 0)
            {
                // Assign the next id, self-healing a corrupt/legacy NextId that isn't already past
                // the highest id in use (mirrors EnsureIds). This guarantees a new profile never
                // collides with an existing one even when SetProfile runs before EnsureIds.
                var maxId = Profiles.Count == 0 ? 0 : Profiles.Max(p => p?.Id ?? 0);
                var next = Math.Max(Math.Max(NextId, 1), maxId + 1);
                profile.Id = next;
                NextId = next + 1;
            }
            else
            {
                var existing = GetById(profile.Id);
                if (existing != null)
                {
                    Profiles.Remove(existing);
                }

                if (NextId <= profile.Id)
                {
                    NextId = profile.Id + 1;
                }
            }

            profile.Touch();
            Profiles.Add(profile);
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes a profile by its stable id.
        /// </summary>
        public bool RemoveProfile(int id)
        {
            var profile = GetById(id);
            if (profile != null)
            {
                Profiles.Remove(profile);
                LastUpdated = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        /// <summary>
        /// One-shot upgrade: assigns a stable id to every profile still missing one (Id == 0), in
        /// list order, and advances NextId past the highest id in use (self-healing a corrupt or
        /// legacy counter). Returns true when anything changed. Idempotent on subsequent calls.
        /// </summary>
        public bool EnsureIds()
        {
            var changed = false;

            var maxId = Profiles.Count == 0 ? 0 : Profiles.Max(p => p?.Id ?? 0);
            var next = Math.Max(Math.Max(NextId, 1), maxId + 1);

            foreach (var p in Profiles)
            {
                if (p is not null && p.Id == 0)
                {
                    p.Id = next++;
                    changed = true;
                }
            }

            if (NextId != next)
            {
                NextId = next;
                changed = true;
            }

            return changed;
        }
    }
}
