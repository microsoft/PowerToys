// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Renders an Adaptive Cards <c>ProgressBar</c> element with the native WinUI progress bar, so
/// value changes animate and assistive technology reads the value.
/// </summary>
internal sealed partial class ProgressBarControl : AdaptiveVisualControl
{
    private readonly ProgressBar _bar = new()
    {
        Minimum = 0,
        MinHeight = 4,
        Height = 4,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 4, 0, 4),
    };

    private ProgressBarModel _model;

    public ProgressBarControl(ProgressBarModel model)
    {
        _model = model;
        Content = _bar;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override bool WidthAffectsLayout => false;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((ProgressBarControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        _bar.Maximum = _model.Max;
        _bar.IsIndeterminate = _model.IsIndeterminate;
        if (!_model.IsIndeterminate)
        {
            _bar.Value = _model.ClampedValue;
        }

        var color = ChartTheme.ResolveSemantic(_model.Color ?? ChartPalette.AccentColorName, isDarkTheme)
            ?? ChartTheme.GetAccent(isDarkTheme);
        _bar.Foreground = ChartTheme.ToBrush(color);
        _bar.Background = ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme));
    }
}
