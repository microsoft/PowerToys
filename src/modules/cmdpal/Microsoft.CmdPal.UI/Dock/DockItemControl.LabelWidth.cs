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
    private double? _characterWidth;
    private UISettings? _textSettings;
    private long[]? _labelFontCallbackTokens;

    private static void OnLabelWidthConstraintsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DockItemControl)d).UpdateLabelWidth();
    }

    private void InitializeLabelWidth()
    {
        StopWatchingLabelFont();
        _textPanel = GetTemplateChild("TextPanel") as FrameworkElement;
        _titleText = GetTemplateChild("TitleText") as TextBlock;
        _characterWidth = null;
    }

    private void UpdateLabelWidth()
    {
        if (_textPanel is null || _titleText is null)
        {
            return;
        }

        var hasVisibleText = TextVisibility == Visibility.Visible && (HasTitle || (HasSubtitle && !IsCompact));
        var constraints = hasVisibleText ? LabelWidthConstraints ?? DockLabelWidthConstraints.Default : DockLabelWidthConstraints.Default;
        if (constraints.UsesCharacters && IsLoaded)
        {
            WatchLabelFont();
        }
        else
        {
            StopWatchingLabelFont();
        }

        // Ordinary label updates reuse this measurement. Only a new template, font, or text scale invalidates it.
        var characterWidth = constraints.UsesCharacters ? _characterWidth ??= MeasureCharacterWidth() : 0;
        var defaultMinimum = hasVisibleText && HasTitle ? 24 : 0;
        var (minimum, maximum) = constraints.Resolve(characterWidth, defaultMinimum, 100);

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

    private double MeasureCharacterWidth()
    {
        _textSettings ??= new UISettings();
        var title = _titleText!;
        var textScale = title.IsTextScaleFactorEnabled ? _textSettings.TextScaleFactor : 1;
        var measure = new TextBlock
        {
            Text = "0",
            FontFamily = title.FontFamily,
            FontSize = title.FontSize * textScale,
            FontWeight = title.FontWeight,
            FontStyle = title.FontStyle,
            FontStretch = title.FontStretch,
            CharacterSpacing = title.CharacterSpacing,
            Language = title.Language,
            FlowDirection = title.FlowDirection,
            IsTextScaleFactorEnabled = false,
            UseLayoutRounding = false,
        };
        Typography.SetNumeralAlignment(measure, Typography.GetNumeralAlignment(title));
        Typography.SetNumeralStyle(measure, Typography.GetNumeralStyle(title));
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return measure.DesiredSize.Width;
    }

    private void WatchLabelFont()
    {
        if (_labelFontCallbackTokens is not null || _titleText is null)
        {
            return;
        }

        _labelFontCallbackTokens = new long[LabelFontProperties.Length];
        for (var i = 0; i < LabelFontProperties.Length; i++)
        {
            _labelFontCallbackTokens[i] = _titleText.RegisterPropertyChangedCallback(LabelFontProperties[i], OnLabelFontChanged);
        }

        _textSettings ??= new UISettings();
        _textSettings.TextScaleFactorChanged += TextSettings_TextScaleFactorChanged;
    }

    private void StopWatchingLabelFont()
    {
        if (_labelFontCallbackTokens is null)
        {
            return;
        }

        for (var i = 0; i < LabelFontProperties.Length; i++)
        {
            _titleText?.UnregisterPropertyChangedCallback(LabelFontProperties[i], _labelFontCallbackTokens[i]);
        }

        _labelFontCallbackTokens = null;
        _textSettings!.TextScaleFactorChanged -= TextSettings_TextScaleFactorChanged;
        _characterWidth = null;
    }

    private void OnLabelFontChanged(DependencyObject sender, DependencyProperty dp) => InvalidateLabelFont();

    private void InvalidateLabelFont()
    {
        _characterWidth = null;
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
