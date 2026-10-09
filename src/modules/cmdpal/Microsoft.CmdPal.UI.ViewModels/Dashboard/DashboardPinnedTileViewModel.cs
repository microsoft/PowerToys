// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dashboard;

/// <summary>
/// A row in the settings page's list of pinned dashboard tiles.
/// </summary>
public sealed record DashboardPinnedTileViewModel(
    string ProviderId,
    string CommandId,
    string Title,
    string Subtitle,
    IconInfoViewModel? Icon)
{
    public bool Matches(DashboardTileSettings tile) => tile.Matches(ProviderId, CommandId);
}
