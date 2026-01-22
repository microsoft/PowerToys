// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

/// <summary>
/// Strongly typed application-level settings for the Windows Terminal extension.
/// These are distinct from the dynamic command palette <see cref="JsonSettingsManager"/> based settings
/// and are meant for simple persisted state (e.g. last selections).
/// </summary>
public sealed class AppSettings
{
    private const int MaxRecentProfilesCount = 64;

    /// <summary>
    /// Gets or sets the last selected channel identifier for the Windows Terminal extension.
    /// Empty string when no channel has been selected yet.
    /// </summary>
    [JsonPropertyName("lastSelectedChannel")]
    public string LastSelectedChannel { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of recently used profile identifiers.
    /// </summary>
    [JsonPropertyName("recentlyUsedProfiles")]
    public List<TerminalProfileKey> RecentlyUsedProfiles { get; init; } = [];

    public void AddRecentlyUsedProfile(string appId, string profileName)
    {
        var key = new TerminalProfileKey(appId, profileName);
        RecentlyUsedProfiles.Remove(key);
        RecentlyUsedProfiles.Insert(0, key);

        if (RecentlyUsedProfiles.Count > MaxRecentProfilesCount)
        {
            RecentlyUsedProfiles.RemoveRange(MaxRecentProfilesCount, RecentlyUsedProfiles.Count - MaxRecentProfilesCount);
        }
    }
}
