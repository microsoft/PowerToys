// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using ManagedCommon;
using Microsoft.CmdPal.UI.Controls.AdaptiveCards;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContentFormControl : UserControl
{
    private static readonly AdaptiveCardRenderer _renderer;
    private ContentFormViewModel? _viewModel;

    // LOAD-BEARING: if you don't hang onto a reference to the RenderedAdaptiveCard
    // then the GC might clean it up sometime, even while the card is in the UI
    // tree. If this gets GC'ed, then it'll revoke our Action handler, and the
    // form will do seemingly nothing.
    private RenderedAdaptiveCard? _renderedCard;

    public ContentFormViewModel? ViewModel { get => _viewModel; set => AttachViewModel(value); }

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(int),
            typeof(ContentFormControl),
            new PropertyMetadata(8));

    public int ItemSpacing
    {
        get => (int)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    static ContentFormControl()
    {
        // We can't use `CardOverrideStyles` here yet, because we haven't called InitializeComponent once.
        // But also, the default value isn't `null` here. It's... some other default empty value.
        // So clear it out so that we know when the first time we get created is
        _renderer = new AdaptiveCardRenderer()
        {
            OverrideStyles = null,
        };

        // concrete
        Register<AdaptiveSettingsToggleInputElement, AdaptiveSettingsToggleInputParser, AdaptiveSettingsToggleInputElementRenderer>();
        Register<AdaptiveSettingsCheckBoxInputElement, AdaptiveSettingsCheckBoxInputParser, AdaptiveSettingsCheckBoxInputRenderer>();
        Register<AdaptiveSettingsTextInputElement, AdaptiveSettingsTextInputParser, AdaptiveSettingsTextInputElementRenderer>();
        Register<AdaptiveSettingsTextAreaElement, AdaptiveSettingsTextAreaParser, AdaptiveSettingsTextAreaElementRenderer>();
        Register<AdaptiveSettingsComboBoxInputElement, AdaptiveSettingsComboBoxInputParser, AdaptiveSettingsComboBoxInputRenderer>();

        // generic
        Register<AdaptiveSettingsExpanderElement, AdaptiveSettingsExpanderElementParser, AdaptiveSettingsExpanderElementRenderer>();
        Register<AdaptiveSettingsCardContainerElement, AdaptiveSettingsCardContainerElementParser, AdaptiveSettingsCardContainerElementRenderer>();

        return;

        static void Register<TElement, TParser, TRenderer>()
            where TElement : IAdaptiveCardElement, ICustomAdaptiveCardElement
            where TParser : IAdaptiveElementParser, new()
            where TRenderer : IAdaptiveElementRenderer, new()
        {
            ContentFormViewModel.AdaptiveElementParserRegistration.Set(TElement.CustomInputType, new TParser());
            _renderer.ElementRenderers.Set(TElement.CustomInputType, new TRenderer());
        }
    }

    public static void Initialize()
    {
    }

    public ContentFormControl()
    {
        this.InitializeComponent();
        var lightTheme = ActualTheme == Microsoft.UI.Xaml.ElementTheme.Light;
        _renderer.HostConfig = lightTheme ? AdaptiveCardsConfig.Light : AdaptiveCardsConfig.Dark;
        _renderer.HostConfig.Spacing.Padding = 0;

        // 5% BODGY: if we set this multiple times over the lifetime of the app,
        // then the second call will explode, because "CardOverrideStyles is already the child of another element".
        // SO only set this once.
        if (_renderer.OverrideStyles is null)
        {
            _renderer.OverrideStyles = CardOverrideStyles;
        }

        // TODO in the future, we should handle ActualThemeChanged and replace
        // our rendered card with one for that theme. But today is not that day
    }

    private void AttachViewModel(ContentFormViewModel? vm)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = vm;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            var c = _viewModel.Card;
            if (c is not null)
            {
                DisplayCard(c);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModel.Card))
        {
            var c = ViewModel.Card;
            if (c is not null)
            {
                DisplayCard(c);
            }
        }
    }

    private void DisplayCard(AdaptiveCardParseResult result)
    {
        // Apply configured item spacing before rendering
        _renderer.HostConfig.Spacing.Default = (uint)ItemSpacing;

        /*
         * ########################################################
         * TODO:
         * ########################################################
         */

        // This will remove extra spacing labels and errors, since we want a more compact form layout
        // to keep Settings cards look good and spaced uniformly.
        // On the other hand, collapsing this with break normal forms (without settings card) .
        _renderer.HostConfig.Inputs.ErrorMessage.Spacing = Spacing.None;
        _renderer.HostConfig.Inputs.Label.InputSpacing = Spacing.None;

        // Print the warnings to the debug console, since they can be helpful for debugging but aren't surfaced in the UI
        foreach (var warning in result.Warnings)
        {
            Logger.LogWarning($"Adaptive cards: {warning.StatusCode}: {warning.Message}");
        }

        foreach (var error in result.Errors)
        {
            Logger.LogError($"Adaptive cards: {error.StatusCode}: {error.Message}");
        }

        _renderedCard = _renderer.RenderAdaptiveCard(result.AdaptiveCard);

        // Print the warnings to the debug console, since they can be helpful for debugging but aren't surfaced in the UI
        foreach (var warning in _renderedCard.Warnings)
        {
            Logger.LogWarning($"Rendered adaptive card: {warning.StatusCode}: {warning.Message}");
        }

        foreach (var error in _renderedCard.Errors)
        {
            Logger.LogError($"Rendered adaptive card: {error.StatusCode}: {error.Message}");
        }

        ContentGrid.Children.Clear();
        if (_renderedCard.FrameworkElement is not null)
        {
            ContentGrid.Children.Add(_renderedCard.FrameworkElement);

            // Use the Loaded event to ensure we focus after the card is in the visual tree
            _renderedCard.FrameworkElement.Loaded += OnFrameworkElementLoaded;
        }

        _renderedCard.Action += Rendered_Action;
    }

    private void OnFrameworkElementLoaded(object sender, RoutedEventArgs e)
    {
        // Unhook the event handler to avoid multiple registrations
        if (sender is FrameworkElement element)
        {
            element.Loaded -= OnFrameworkElementLoaded;

            if (!ViewModel?.OnlyControlOnPage ?? true)
            {
                return;
            }

            // Focus on the first focusable element asynchronously to ensure the visual tree is fully built
            element.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                var focusableElement = FindFirstFocusableElement(element);
                focusableElement?.Focus(FocusState.Programmatic);
            });
        }
    }

    private Control? FindFirstFocusableElement(DependencyObject parent)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);

        // Process children first (depth-first search)
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // If the child is a focusable control like TextBox, ComboBox, etc.
            if (child is Control control &&
                control.IsEnabled &&
                control.IsTabStop &&
                control.Visibility == Visibility.Visible &&
                control.AllowFocusOnInteraction)
            {
                return control;
            }

            // Recursively check children
            var result = FindFirstFocusableElement(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void Rendered_Action(RenderedAdaptiveCard sender, AdaptiveActionEventArgs args) =>
        ViewModel?.HandleSubmit(args.Action, args.Inputs.AsJson());
}
