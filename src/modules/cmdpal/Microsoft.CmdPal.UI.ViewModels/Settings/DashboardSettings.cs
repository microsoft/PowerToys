// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// Settings for the dashboard: a transient, full-screen overlay on the primary display that
/// shows pages pinned to it as tiles. The enabled flag and the shortcut live on
/// <see cref="SettingsModel"/>, so changing them doesn't rebuild the dashboard's tiles.
/// </summary>
public record DashboardSettings
{
    public const int MinBackgroundOpacity = 0;
    public const int MaxBackgroundOpacity = 100;
    public const int DefaultBackgroundOpacity = 45;

    /// <summary>
    /// Gets a value indicating whether the dashboard shortcut is ignored while a
    /// full-screen game or other full-screen app (D3D exclusive, borderless, or
    /// presentation mode) is in the foreground.
    /// </summary>
    public bool IgnoreHotkeyWhenFullscreen { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the dashboard dismisses itself when another
    /// window takes focus.
    /// </summary>
    public bool DismissOnFocusLost { get; init; } = true;

    public DashboardBackdrop Backdrop { get; init; } = DashboardBackdrop.Dim;

    /// <summary>
    /// Gets how strongly the desktop is dimmed behind the tiles, from 0 to 100.
    /// </summary>
    public int BackgroundOpacity { get; init; } = DefaultBackgroundOpacity;

    /// <summary>
    /// Gets the grid size, in cells, that the tile positions were last laid out on.
    /// When the display changes, positions are scaled from this grid to the new one.
    /// Zero means "unknown", and positions are used as-is.
    /// </summary>
    public int DesignColumns { get; init; }

    /// <summary>
    /// Gets the grid height, in cells, that the tile positions were last laid out on.
    /// </summary>
    public int DesignRows { get; init; }

    // Uses an EquatableList backing field so record equality compares tiles by content.
    private readonly EquatableList<DashboardTileSettings> _tiles = new(ImmutableList<DashboardTileSettings>.Empty);

    public ImmutableList<DashboardTileSettings> Tiles
    {
        get => _tiles.List;
        init => _tiles = new(value ?? ImmutableList<DashboardTileSettings>.Empty);
    }

    public bool IsPinned(string providerId, string commandId) =>
        IndexOf(providerId, commandId) >= 0;

    public int IndexOf(string providerId, string commandId)
    {
        for (var i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i].Matches(providerId, commandId))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns settings with the command pinned as a new tile. The tile has no position yet;
    /// the layout places it in the first free spot the next time the dashboard is arranged.
    /// Pinning a command that's already pinned returns this instance.
    /// </summary>
    public DashboardSettings WithTilePinned(string providerId, string commandId)
    {
        if (IsPinned(providerId, commandId))
        {
            return this;
        }

        return this with
        {
            Tiles = Tiles.Add(new DashboardTileSettings
            {
                ProviderId = providerId,
                CommandId = commandId,
            }),
        };
    }

    public DashboardSettings WithTileUnpinned(string providerId, string commandId)
    {
        var index = IndexOf(providerId, commandId);
        return index < 0 ? this : this with { Tiles = Tiles.RemoveAt(index) };
    }

    /// <summary>
    /// Returns settings with every tile's position replaced by <paramref name="layout"/>,
    /// recorded against the grid it was arranged on.
    /// </summary>
    public DashboardSettings WithLayout(IReadOnlyList<DashboardTileRect> layout, int columns, int rows)
    {
        if (layout.Count != Tiles.Count)
        {
            throw new ArgumentException("The layout must have one rectangle per tile.", nameof(layout));
        }

        var builder = ImmutableList.CreateBuilder<DashboardTileSettings>();
        for (var i = 0; i < Tiles.Count; i++)
        {
            builder.Add(Tiles[i].WithRect(layout[i]));
        }

        return this with
        {
            Tiles = builder.ToImmutable(),
            DesignColumns = columns,
            DesignRows = rows,
        };
    }

    /// <summary>
    /// Returns settings where every tile goes back to its default size and gets placed
    /// automatically.
    /// </summary>
    public DashboardSettings WithLayoutReset() => this with
    {
        Tiles = Tiles.Select(tile => tile with
        {
            Column = DashboardTileSettings.Unplaced,
            Row = DashboardTileSettings.Unplaced,
            ColumnSpan = DashboardLayout.DefaultColumnSpan,
            RowSpan = DashboardLayout.DefaultRowSpan,
        }).ToImmutableList(),
        DesignColumns = 0,
        DesignRows = 0,
    };
}
