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
                profile => profile is not null && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the profile by its UUID, or null when the id is empty or no profile has it.
        /// </summary>
        public PowerDisplayProfile? GetById(Guid id)
        {
            return id == Guid.Empty ? null : Profiles.FirstOrDefault(profile => profile?.Id == id);
        }

        /// <summary>
        /// Resolves a numeric reference retained in another settings file during UUID migration.
        /// </summary>
        public PowerDisplayProfile? GetByLegacyId(int legacyId)
        {
            return legacyId <= 0 ? null : Profiles.FirstOrDefault(profile => profile?.LegacyId == legacyId);
        }

        /// <summary>
        /// Returns profiles with assigned UUIDs in display order, independent of their position
        /// in the persisted array. Unassigned profiles remain hidden until migration is saved.
        /// </summary>
        public IEnumerable<PowerDisplayProfile> GetAssignedProfiles()
        {
            return Profiles.Where(profile => profile is not null && profile.Id != Guid.Empty)
                .OrderBy(profile => profile.Order);
        }

        /// <summary>
        /// Adds or updates a profile by UUID. New profiles receive a random UUID and are displayed
        /// last. Updating preserves both display order and the legacy reference mapping.
        /// </summary>
        public void SetProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                throw new ArgumentException("Profile is invalid");
            }

            var existingIndex = profile.Id != Guid.Empty ? Profiles.FindIndex(p => p?.Id == profile.Id) : -1;
            if (existingIndex >= 0)
            {
                profile.Order = Profiles[existingIndex].Order;
                profile.LegacyId = Profiles[existingIndex].LegacyId;
            }
            else
            {
                if (profile.Id == Guid.Empty)
                {
                    do
                    {
                        profile.Id = Guid.NewGuid();
                    }
                    while (GetById(profile.Id) is not null);
                }

                profile.Order = checked(Profiles.Where(p => p is not null).Select(p => p.Order).DefaultIfEmpty(-1).Max() + 1);
                profile.LegacyId = null;
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
        /// Moves a profile before another UUID, or to the end when beforeProfileId is null.
        /// Returns false without changing the collection when either id is invalid or missing,
        /// or the profile is already at the requested position. Only display order is changed;
        /// the persisted array and profile contents retain their original positions and values.
        /// </summary>
        public bool MoveProfileBefore(Guid profileId, Guid? beforeProfileId)
        {
            if (profileId == Guid.Empty || beforeProfileId == Guid.Empty || profileId == beforeProfileId)
            {
                return false;
            }

            var ordered = GetAssignedProfiles().ToList();
            var sourceIndex = ordered.FindIndex(profile => profile.Id == profileId);
            var targetIndex = beforeProfileId.HasValue
                ? ordered.FindIndex(profile => profile.Id == beforeProfileId.Value)
                : ordered.Count;
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

            var profile = ordered[sourceIndex];
            ordered.RemoveAt(sourceIndex);
            ordered.Insert(targetIndex, profile);
            SetOrder(ordered);
            LastUpdated = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Removes a profile by UUID and closes the gap in display order.
        /// </summary>
        public bool RemoveProfile(Guid id)
        {
            var profile = GetById(id);
            if (profile != null)
            {
                Profiles.Remove(profile);
                SetOrder(GetAssignedProfiles().ToList());
                LastUpdated = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Assigns random UUIDs to missing or duplicate identities and repairs display order.
        /// Missing orders fall back to array positions; ties retain array order. The array itself
        /// is unchanged, and numeric migration references remain assigned to their first owner.
        /// Returns true when anything changed. The caller must save before publishing new UUIDs.
        /// </summary>
        public bool EnsureIdsAndOrder()
        {
            var changed = false;
            var assignedIds = new HashSet<Guid>();
            var legacyIds = new HashSet<int>();
            foreach (var profile in Profiles)
            {
                if (profile is null)
                {
                    continue;
                }

                if (profile.Id == Guid.Empty || !assignedIds.Add(profile.Id))
                {
                    do
                    {
                        profile.Id = Guid.NewGuid();
                    }
                    while (!assignedIds.Add(profile.Id));
                    changed = true;
                }

                if (profile.LegacyId.HasValue
                    && (profile.LegacyId.Value <= 0 || !legacyIds.Add(profile.LegacyId.Value)))
                {
                    profile.LegacyId = null;
                    changed = true;
                }
            }

            var ordered = Profiles.Select((profile, index) => (Profile: profile, Index: index))
                .Where(item => item.Profile is not null)
                .OrderBy(item => item.Profile.Order >= 0 ? item.Profile.Order : item.Index)
                .ThenBy(item => item.Index)
                .Select(item => item.Profile)
                .ToList();
            return SetOrder(ordered) || changed;
        }

        private static bool SetOrder(List<PowerDisplayProfile> ordered)
        {
            var changed = false;
            for (var index = 0; index < ordered.Count; index++)
            {
                if (ordered[index].Order != index)
                {
                    ordered[index].Order = index;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
