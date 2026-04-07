// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Provides secondary content for a dock item that is displayed via a
/// Live-Tile-style flip animation. Extensions set the properties on this
/// object and the dock UI will periodically flip between the primary
/// content and this live tile content.
/// </summary>
public partial class LiveTileContent : BaseObservable, ILiveTileContent
{
    public virtual IIconInfo? Icon { get; set => SetProperty(ref field, value); }

    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Subtitle { get; set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Gets or sets the interval in milliseconds between flips.
    /// A value of 0 (or negative) disables the flip animation.
    /// Default: 5000ms (5 seconds).
    /// </summary>
    public virtual int FlipIntervalMs { get; set => SetProperty(ref field, value); } = 5000;

    public LiveTileContent()
    {
    }

    public LiveTileContent(string title, string subtitle = "", IIconInfo? icon = null, int flipIntervalMs = 5000)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        FlipIntervalMs = flipIntervalMs;
    }
}
