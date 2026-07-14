// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

public static class ProfileMigration
{
    public static bool Migrate(
        PowerDisplayProfiles profiles,
        IReadOnlyList<(string Id, int MonitorNumber)> discovered)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(discovered);

        var changed = profiles.EnsureIds();
        if (discovered.Count == 0)
        {
            return changed;
        }

        foreach (var profile in profiles.Profiles)
        {
            if (profile?.MonitorSettings is null)
            {
                continue;
            }

            var profileChanged = false;
            foreach (var legacy in profile.MonitorSettings
                .Where(setting => MonitorIdentity.IsLegacyId(setting?.MonitorId))
                .ToList())
            {
                var newId = MonitorIdMigrator.MatchNewId(legacy.MonitorId, discovered);
                if (newId != null
                    && profile.MonitorSettings.All(
                        setting => !MonitorIdComparer.Equal(setting.MonitorId, newId)))
                {
                    profile.MonitorSettings.Add(new ProfileMonitorSetting(
                        newId,
                        legacy.Brightness,
                        legacy.ColorTemperatureVcp,
                        legacy.Contrast,
                        legacy.Volume));
                }
                else if (newId != null)
                {
                    Logger.LogInfo(
                        $"[LegacyMigration] Skipped duplicate profile setting for '{legacy.MonitorId}' in profile '{profile.Name}': '{newId}' already exists.");
                }
                else if (newId == null)
                {
                    Logger.LogWarning(
                        $"[LegacyMigration] Dropping profile setting for '{legacy.MonitorId}' in profile '{profile.Name}': no current monitor with matching EdidId+MonitorNumber.");
                }

                profile.MonitorSettings.Remove(legacy);
                profileChanged = true;
            }

            if (profileChanged)
            {
                profile.Touch();
                changed = true;
            }
        }

        return changed;
    }
}
