// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleSwatchIconPage : ListPage
{
    private const int HueCount = 16;
    private const int ToneCount = 16;
    private IListItem[] _items;

    public SampleSwatchIconPage()
    {
        Icon = new IconInfo("|Swatch|#0067C0|#60CDFF|square|");
        Name = "Swatch Icon Palette";
        GridProperties = new SmallGridLayout();
    }

    public override IListItem[] GetItems() => _items ??= CreatePaletteItems();

    private static IListItem[] CreatePaletteItems()
    {
        var items = new IListItem[HueCount * ToneCount];
        for (var hueIndex = 0; hueIndex < HueCount; hueIndex++)
        {
            var hue = hueIndex * 360d / HueCount;
            for (var toneIndex = 0; toneIndex < ToneCount; toneIndex++)
            {
                var index = (hueIndex * ToneCount) + toneIndex;
                var saturation = 0.60 + (0.20 * Math.Sin(Math.PI * toneIndex / (ToneCount - 1)));
                var lightness = 0.24 + (0.56 * toneIndex / (ToneCount - 1));
                var darkLightness = Math.Min(0.86, lightness + (lightness < 0.55 ? 0.12 : 0.05));
                var light = ToHexColor(hue, saturation, lightness);
                var dark = ToHexColor(hue, saturation, darkLightness);
                var shape = ((hueIndex + toneIndex) & 1) == 0 ? "circle" : "square";
                var protocol = $"|Swatch|{light}|{dark}|{shape}|";
                var icon = new IconInfo(protocol);

                items[index] = new ListItem(new CopyTextCommand(protocol) { Name = "Copy swatch protocol" })
                {
                    Title = $"{index + 1:D3}  {light}",
                    Subtitle = $"{shape} · dark {dark}",
                    Icon = icon,
                };
            }
        }

        return items;
    }

    private static string ToHexColor(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var hueSection = hue / 60;
        var secondary = chroma * (1 - Math.Abs((hueSection % 2) - 1));
        var (red, green, blue) = (int)hueSection switch
        {
            0 => (chroma, secondary, 0d),
            1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary),
            3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary),
        };
        var match = lightness - (chroma / 2);

        return $"#{ToByte(red + match):X2}{ToByte(green + match):X2}{ToByte(blue + match):X2}";
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * byte.MaxValue, MidpointRounding.AwayFromZero);
}
