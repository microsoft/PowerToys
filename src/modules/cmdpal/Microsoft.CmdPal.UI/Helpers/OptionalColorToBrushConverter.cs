// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Color = Windows.UI.Color;

namespace Microsoft.CmdPal.UI.Helpers;

public static partial class OptionalColorBrushCacheProvider
{
    private static readonly Dictionary<OptionalColor, SolidColorBrush> _brushCache = [];

    //// TODO: Not sure how best to make this configurable or provide a themed resource here... (mostly due to shortcomings of x:Bind/MarkupExtension, need to file many bugs)
    private static readonly SolidColorBrush _defaultBackgroundBrush = new(Colors.Black);
    private static readonly SolidColorBrush _defaultForegroundBrush = new(Colors.White);

    public static SolidColorBrush? Convert(OptionalColor color, bool isForeground)
    {
        if (!color.HasValue)
        {
            return isForeground ? _defaultForegroundBrush : _defaultBackgroundBrush;
        }

        if (!_brushCache.TryGetValue(color, out var brush))
        {
            // Create and cache the brush if we see this color for the first time.
            brush = new SolidColorBrush(Color.FromArgb(color.Color.A, color.Color.R, color.Color.G, color.Color.B));
            _brushCache[color] = brush;
        }

        return brush;
    }
}
