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
        /// Returns profiles with usable stable ids in their persisted array order.
        /// Legacy or corrupt profiles with non-positive ids remain hidden until migration.
        /// </summary>
        public IEnumerable<PowerDisplayProfile> GetAssignedProfiles()
        {
            return Profiles.Where(profile => profile is not null && profile.Id >= 1);
        }

        /// <summary>
        /// Adds or updates a profile by its stable id. New profiles receive an id from the monotonic
        /// NextId counter and are displayed last. Updating preserves the profile's display order.
        /// </summary>
        public void SetProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                throw new ArgumentException("Profile is invalid");
            }

            var existingIndex = profile.Id >= 1 ? Profiles.FindIndex(p => p?.Id == profile.Id) : -1;
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
            else if (NextId <= profile.Id)
            {
                NextId = profile.Id + 1;
            }

            profile.Touch();
            if (existingIndex >= 0)
            {
                Profiles[existingIndex] = profile;
            }
            else
            {
                Profiles.Add(profile);
            }

            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Moves a profile before another id, or to the end when beforeProfileId is null.
        /// Returns false without changing the collection when either id is invalid or missing,
        /// or the profile is already at the requested position. Moves the existing array entry
        /// without changing its stable id, contents, or timestamps.
        /// </summary>
        public bool MoveProfileBefore(int profileId, int? beforeProfileId)
        {
            if (profileId <= 0 || beforeProfileId is <= 0 || profileId == beforeProfileId)
            {
                return false;
            }

            var sourceIndex = Profiles.FindIndex(profile => profile?.Id == profileId);
            var targetIndex = beforeProfileId.HasValue
                ? Profiles.FindIndex(profile => profile?.Id == beforeProfileId.Value)
                : Profiles.Count;
            if (sourceIndex < 0 || targetIndex < 0)
            {
                return false;
            }

            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }

            if (sourceIndex == targetIndex)
            {
                return false;
            }

            var profile = Profiles[sourceIndex];
            Profiles.RemoveAt(sourceIndex);
            Profiles.Insert(targetIndex, profile);
            LastUpdated = DateTime.UtcNow;
            return true;
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
