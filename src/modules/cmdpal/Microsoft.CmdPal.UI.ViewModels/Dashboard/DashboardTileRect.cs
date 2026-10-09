// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Dashboard;

/// <summary>
/// A tile's position and size on the dashboard grid, in cells.
/// A negative <see cref="Column"/> or <see cref="Row"/> means the tile hasn't been placed.
/// </summary>
public readonly record struct DashboardTileRect(int Column, int Row, int ColumnSpan, int RowSpan)
{
    public int Right => Column + ColumnSpan;

    public int Bottom => Row + RowSpan;

    public bool IsPlaced => Column >= 0 && Row >= 0;

    public bool IntersectsWith(DashboardTileRect other) =>
        Column < other.Right && other.Column < Right &&
        Row < other.Bottom && other.Row < Bottom;
}
