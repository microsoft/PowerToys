// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Renders an Adaptive Cards <c>Icon</c> element as a Segoe Fluent Icons glyph.</summary>
internal sealed partial class IconControl : AdaptiveVisualControl
{
    private IconModel _model;

    public IconControl(IconModel model)
    {
        _model = model;
        VerticalAlignment = VerticalAlignment.Center;

        // Icons have no text alternative in the schema, so they're decorative.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override bool WidthAffectsLayout => false;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((IconControl)candidate)._model;

    protected override void RenderCore()
    {
        HorizontalAlignment = _model.HorizontalAlignment switch
        {
            IconAlignment.Center => HorizontalAlignment.Center,
            IconAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
        if (_model.Glyph is null)
        {
            Content = null;
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        Content = new FontIcon
        {
            Glyph = _model.Glyph,
            FontSize = _model.PixelSize,
            Foreground = ChartTheme.ToBrush(GetColor(_model.Color, IsDarkTheme)),
        };
    }

    /// <summary>Resolves an icon color the way text colors resolve in the host config.</summary>
    private static ChartColor GetColor(IconColor color, bool isDarkTheme)
    {
        var text = ChartTheme.GetTextColor(isDarkTheme, secondary: false);
        if (ChartTheme.IsHighContrast)
        {
            return color == IconColor.Accent ? ChartTheme.GetHighContrastColor() : text;
        }

        return color switch
        {
            IconColor.Dark => new ChartColor(0xE4, 0x00, 0x00, 0x00),
            IconColor.Light => new ChartColor(0xFF, 0xFF, 0xFF, 0xFF),
            IconColor.Accent => ChartTheme.ResolveSemantic("accent", isDarkTheme) ?? text,
            IconColor.Good => ChartTheme.ResolveSemantic("good", isDarkTheme) ?? text,
            IconColor.Warning => ChartTheme.ResolveSemantic("warning", isDarkTheme) ?? text,
            IconColor.Attention => ChartTheme.ResolveSemantic("attention", isDarkTheme) ?? text,
            _ => text,
        };
    }
}
