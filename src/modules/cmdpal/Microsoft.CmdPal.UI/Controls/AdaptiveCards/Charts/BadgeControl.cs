// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Renders an Adaptive Cards <c>Badge</c> element as a small status pill.</summary>
internal sealed partial class BadgeControl : AdaptiveVisualControl
{
    private BadgeModel _model;

    public BadgeControl(BadgeModel model)
    {
        _model = model;
        HorizontalAlignment = HorizontalAlignment.Left;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override bool WidthAffectsLayout => false;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((BadgeControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        var (fontSize, padding) = _model.Size switch
        {
            BadgeSize.ExtraLarge => (16d, new Thickness(10, 3, 10, 4)),
            BadgeSize.Large => (14d, new Thickness(8, 2, 8, 3)),
            _ => (12d, new Thickness(6, 1, 6, 2)),
        };

        var text = new TextBlock
        {
            Text = _model.Text,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var badge = new Border
        {
            Padding = padding,
            Child = text,
            CornerRadius = _model.Shape switch
            {
                BadgeShape.Square => new CornerRadius(2),
                BadgeShape.Circular => new CornerRadius(fontSize * 2),
                _ => new CornerRadius(4),
            },
        };

        // Badges are neutral unless they ask for a semantic style.
        var color = ChartTheme.ResolveSemantic(_model.Style ?? "default", isDarkTheme);
        if (color is ChartColor semantic)
        {
            if (_model.Appearance == BadgeAppearance.Tint)
            {
                badge.Background = ChartTheme.ToBrush(semantic.WithOpacity(isDarkTheme ? 0.22 : 0.14));
                badge.BorderBrush = ChartTheme.ToBrush(semantic.WithOpacity(0.45));
                badge.BorderThickness = new Thickness(1);
                text.Foreground = ChartTheme.ToBrush(semantic);
            }
            else
            {
                badge.Background = ChartTheme.ToBrush(semantic);
                text.Foreground = ChartTheme.ToBrush(ChartTheme.GetContrastingText(semantic));
            }
        }
        else
        {
            badge.Background = ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme));
            text.Foreground = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: false));
        }

        if (!string.IsNullOrWhiteSpace(_model.Tooltip))
        {
            ToolTipService.SetToolTip(badge, _model.Tooltip);
        }

        Content = badge;
        AutomationProperties.SetName(this, _model.Text);
    }
}
