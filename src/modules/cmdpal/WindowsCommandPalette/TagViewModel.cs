// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WindowsCommandPalette;

public sealed class TagViewModel
{
    private readonly ITag _tag;

    internal IconDataType Icon => _tag.Icon;

    internal string Text => _tag.Text;

    public bool HasIcon => !string.IsNullOrEmpty(Icon?.Icon);

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon?.Icon ?? string.Empty, 10);

    public Windows.UI.Color Color
    {
        get
        {
            var color = _tag.Color;
            if (color.HasValue)
            {
                var c = color.Color;
                return Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            return default;
        }
    }

    // TODO! VV These guys should have proper theme-aware lookups for default values
    // All this code is exceptionally terrible, but it's just here to keep the POC app running at this point.
    internal Brush BorderBrush => new SolidColorBrush(Color);

    internal Brush TextBrush => new SolidColorBrush(Color.A == 0 ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : Color);

    internal Brush BackgroundBrush => new SolidColorBrush(Color.A == 0 ? Color : Windows.UI.Color.FromArgb((byte)(Color.A / 4), Color.R, Color.G, Color.B));

    public TagViewModel(ITag tag)
    {
        this._tag = tag;

        // this.Tag.PropChanged += Tag_PropertyChanged;
    }
}
