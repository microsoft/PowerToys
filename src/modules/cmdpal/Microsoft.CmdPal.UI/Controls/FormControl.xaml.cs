// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class FormControl : UserControl
{
    private static readonly AdaptiveCardRenderer _renderer;
    private FormViewModel? _viewModel;

    public FormViewModel? ViewModel { get => _viewModel; set => AttachViewModel(value); }

    static FormControl()
    {
        // yep this is the way to check if you're in light theme or dark.
        // yep it's this dumb
        var settings = new UISettings();
        var foreground = settings.GetColorValue(UIColorType.Foreground);
        var lightTheme = foreground.R < 128;
        _renderer = new AdaptiveCardRenderer
        {
            HostConfig = lightTheme ? AdaptiveCardsConfig.Light : AdaptiveCardsConfig.Dark,
        };
    }

    public FormControl()
    {
        this.InitializeComponent();
    }

    private void AttachViewModel(FormViewModel? vm)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = vm;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            var c = _viewModel.Card;
            if (c != null)
            {
                DisplayCard(c);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModel.Card))
        {
            var c = ViewModel.Card;
            if (c != null)
            {
                DisplayCard(c);
            }
        }
    }

    private void DisplayCard(AdaptiveCardParseResult result)
    {
        var rendered = _renderer.RenderAdaptiveCard(result.AdaptiveCard);
        rendered.Action += Rendered_Action;
        ContentGrid.Children.Clear();
        ContentGrid.Children.Add(rendered.FrameworkElement);
    }

    private void Rendered_Action(RenderedAdaptiveCard sender, AdaptiveActionEventArgs args) =>
        ViewModel?.HandleSubmit(args.Action, args.Inputs.AsJson());
}
