// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Contracts;
using PowerDisplay.Models;

namespace PowerDisplay.Ipc;

/// <summary>
/// Pure-function projector that converts the app's profile model into the flat Contracts DTO used
/// by the CLI IPC renderer for the <c>profiles</c> command.
/// </summary>
public static class ProfileDtoProjector
{
    // ─── profiles (list) ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the result DTO for the <c>profiles</c> command.
    /// Projects each <see cref="PowerDisplayProfile"/> in <paramref name="profiles"/> to a
    /// <see cref="CliProfileInfo"/>:
    /// <list type="bullet">
    ///   <item><see cref="CliProfileInfo.Id"/> — profile id.</item>
    ///   <item><see cref="CliProfileInfo.Name"/> — profile name.</item>
    ///   <item><see cref="CliProfileInfo.MonitorCount"/> — <c>MonitorSettings.Count</c>.</item>
    ///   <item><see cref="CliProfileInfo.LastModified"/> — <see cref="PowerDisplayProfile.LastModified"/>
    ///         formatted as ISO 8601 round-trip ("o") with invariant culture.</item>
    /// </list>
    /// </summary>
    /// <param name="profiles">The loaded profiles collection (may be empty; must not be null).</param>
    public static CliProfileListResult BuildProfileListResult(PowerDisplayProfiles profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var infos = new List<CliProfileInfo>(profiles.Profiles.Count);
        foreach (var profile in profiles.GetAssignedProfiles())
        {
            infos.Add(new CliProfileInfo
            {
                Id = profile.Id,
                Name = profile.Name ?? string.Empty,
                MonitorCount = profile.MonitorSettings?.Count ?? 0,

                // ISO 8601 round-trip ("o") with invariant culture.
                LastModified = profile.LastModified.ToString("o", CultureInfo.InvariantCulture),
            });
        }

        return new CliProfileListResult { Profiles = infos };
    }
}
