// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// A page pinned to the dashboard, and where its tile sits. Positions and sizes are in
/// grid cells (see <see cref="DashboardLayout.CellSize"/>).
/// </summary>
public record DashboardTileSettings
{
    /// <summary>
    /// The position used for a tile the user hasn't placed yet.
    /// </summary>
    public const int Unplaced = -1;

    public required string ProviderId { get; init; }

    public required string CommandId { get; init; }

    public int Column { get; init; } = Unplaced;

    public int Row { get; init; } = Unplaced;

    public int ColumnSpan { get; init; } = DashboardLayout.DefaultColumnSpan;

    public int RowSpan { get; init; } = DashboardLayout.DefaultRowSpan;

    [JsonIgnore]
    public DashboardTileRect Rect => new(Column, Row, ColumnSpan, RowSpan);

    public bool Matches(string providerId, string commandId) =>
        string.Equals(ProviderId, providerId, StringComparison.Ordinal) &&
        string.Equals(CommandId, commandId, StringComparison.Ordinal);

    public DashboardTileSettings WithRect(DashboardTileRect rect) => this with
    {
        Column = rect.Column,
        Row = rect.Row,
        ColumnSpan = rect.ColumnSpan,
        RowSpan = rect.RowSpan,
    };
}
