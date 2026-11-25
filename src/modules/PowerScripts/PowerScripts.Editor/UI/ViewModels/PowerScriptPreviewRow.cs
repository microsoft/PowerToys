// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;

namespace PowerScripts.UI.ViewModels;

/// <summary>A read-only row in the new script page's summary cards, such as a place the script shows up or a value it asks for.</summary>
public sealed class PowerScriptPreviewRow
{
    /// <summary>Gets or sets the row's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the secondary line under the title.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the image shown in the row's tile, or null to show <see cref="Glyph"/>.</summary>
    public ImageSource Icon { get; set; }

    /// <summary>Gets or sets the glyph shown in the row's tile when it has no image.</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>Gets or sets the tile hue.</summary>
    public string TileKind { get; set; } = "slate";

    /// <summary>Gets or sets a value indicating whether the place the row stands for is turned off.</summary>
    public bool IsOff { get; set; }

    /// <summary>Gets or sets a value indicating whether a divider is drawn above the row.</summary>
    public bool HasDivider { get; set; }

    /// <summary>Gets a value indicating whether the row has an image.</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>Gets a value indicating whether the row has a title.</summary>
    public bool HasTitle => !string.IsNullOrEmpty(Title);

    /// <summary>Gets a value indicating whether the row has a secondary line.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
}
