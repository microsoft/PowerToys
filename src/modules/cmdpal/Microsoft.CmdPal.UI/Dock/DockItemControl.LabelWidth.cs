// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockItemControl
{
    public static readonly DependencyProperty LabelWidthConstraintsProperty =
        DependencyProperty.Register(nameof(LabelWidthConstraints), typeof(object), typeof(DockItemControl), new PropertyMetadata(null, OnLabelWidthConstraintsChanged));

    public DockLabelWidthConstraints? LabelWidthConstraints
    {
        get => (DockLabelWidthConstraints?)GetValue(LabelWidthConstraintsProperty);
        set => SetValue(LabelWidthConstraintsProperty, value);
    }

    private static readonly DependencyProperty[] LabelFontProperties =
    [
        TextBlock.FontFamilyProperty,
        TextBlock.FontSizeProperty,
        TextBlock.FontWeightProperty,
        TextBlock.FontStyleProperty,
        TextBlock.FontStretchProperty,
        TextBlock.CharacterSpacingProperty,
        TextBlock.IsTextScaleFactorEnabledProperty,
        LanguageProperty,
        Typography.NumeralAlignmentProperty,
        Typography.NumeralStyleProperty,
    ];

    private FrameworkElement? _textPanel;
    private TextBlock? _titleText;
    private TextBlock? _subtitleText;
    private double? _titleCharacterWidth;
    private double? _subtitleCharacterWidth;
    private UISettings? _textSettings;
    private long[]? _titleFontCallbackTokens;
    private long[]? _subtitleFontCallbackTokens;

    private static void OnLabelWidthConstraintsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DockItemControl)d).UpdateTextVisibility();
    }

    private void InitializeLabelWidth()
    {
        StopWatchingLabelFont();
        _textPanel = GetTemplateChild("TextPanel") as FrameworkElement;
        _titleText = GetTemplateChild("TitleText") as TextBlock;
        _subtitleText = GetTemplateChild("SubtitleText") as TextBlock;
        _titleCharacterWidth = null;
        _subtitleCharacterWidth = null;
    }

    private void UpdateLabelWidth()
    {
        if (_textPanel is null || _titleText is null || _subtitleText is null)
        {
            return;
        }

        var hasVisibleText = TextVisibility == Visibility.Visible && HasText;
        var constraints = hasVisibleText ? LabelWidthConstraints ?? DockLabelWidthConstraints.Default : DockLabelWidthConstraints.Default;
        if (constraints.UsesCharacters && IsLoaded)
        {
            WatchLabelFont();
        }
        else
        {
            StopWatchingLabelFont();
        }

        // Cache each row's font measurement across ordinary label updates.
        var titleCharacterWidth = constraints.UsesCharacters ? _titleCharacterWidth ??= MeasureCharacterWidth(_titleText) : 0;
        var subtitleCharacterWidth = constraints.SubtitleWidth?.InCharacters == true ? _subtitleCharacterWidth ??= MeasureCharacterWidth(_subtitleText) : 0;
        var defaultMinimum = hasVisibleText && HasTitle ? 24 : 0;
        var (minimum, maximum) = constraints.Resolve(titleCharacterWidth, subtitleCharacterWidth, defaultMinimum, 100, ShowTitle, ShowSubtitle && !IsCompact);

        // A vertical Dock owns its width. A provider's reservation must not push the label outside it.
        if (_parentDock?.DockSide is DockSide.Left or DockSide.Right)
        {
            minimum = 0;
        }

        if (_textPanel.MinWidth != minimum)
        {
            _textPanel.MinWidth = minimum;
        }

        if (_textPanel.MaxWidth != maximum)
        {
            _textPanel.MaxWidth = maximum;
        }
    }

    private double MeasureCharacterWidth(TextBlock text)
    {
        _textSettings ??= new UISettings();
        var textScale = text.IsTextScaleFactorEnabled ? _textSettings.TextScaleFactor : 1;
        var measure = new TextBlock
        {
            Text = "0",
            FontFamily = text.FontFamily,
            FontSize = text.FontSize * textScale,
            FontWeight = text.FontWeight,
            FontStyle = text.FontStyle,
            FontStretch = text.FontStretch,
            CharacterSpacing = text.CharacterSpacing,
            Language = text.Language,
            FlowDirection = text.FlowDirection,
            IsTextScaleFactorEnabled = false,
            UseLayoutRounding = false,
        };
        Typography.SetNumeralAlignment(measure, Typography.GetNumeralAlignment(text));
        Typography.SetNumeralStyle(measure, Typography.GetNumeralStyle(text));
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return measure.DesiredSize.Width;
    }

    private void WatchLabelFont()
    {
        if (_titleFontCallbackTokens is not null || _titleText is null || _subtitleText is null)
        {
            return;
        }

        _titleFontCallbackTokens = new long[LabelFontProperties.Length];
        _subtitleFontCallbackTokens = new long[LabelFontProperties.Length];
        for (var i = 0; i < LabelFontProperties.Length; i++)
        {
            _titleFontCallbackTokens[i] = _titleText.RegisterPropertyChangedCallback(LabelFontProperties[i], OnLabelFontChanged);
            _subtitleFontCallbackTokens[i] = _subtitleText.RegisterPropertyChangedCallback(LabelFontProperties[i], OnLabelFontChanged);
        }

        _textSettings ??= new UISettings();
        _textSettings.TextScaleFactorChanged += TextSettings_TextScaleFactorChanged;
    }

    private void StopWatchingLabelFont()
    {
        if (_titleFontCallbackTokens is null || _subtitleFontCallbackTokens is null)
        {
            return;
        }

        for (var i = 0; i < LabelFontProperties.Length; i++)
        {
            _titleText?.UnregisterPropertyChangedCallback(LabelFontProperties[i], _titleFontCallbackTokens[i]);
            _subtitleText?.UnregisterPropertyChangedCallback(LabelFontProperties[i], _subtitleFontCallbackTokens[i]);
        }

        _titleFontCallbackTokens = null;
        _subtitleFontCallbackTokens = null;
        _textSettings!.TextScaleFactorChanged -= TextSettings_TextScaleFactorChanged;
        _titleCharacterWidth = null;
        _subtitleCharacterWidth = null;
    }

    private void OnLabelFontChanged(DependencyObject sender, DependencyProperty dp) => InvalidateLabelFont();

    private void InvalidateLabelFont()
    {
        _titleCharacterWidth = null;
        _subtitleCharacterWidth = null;
        UpdateLabelWidth();
    }

    private void TextSettings_TextScaleFactorChanged(UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded)
            {
                InvalidateLabelFont();
            }
        });
    }
}
