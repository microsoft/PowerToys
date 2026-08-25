// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using ManagedCommon;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;
using Microsoft.CmdPal.UI.Controls.AdaptiveCards;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContentFormControl : UserControl
{
    private readonly AdaptiveCardRenderer _renderer;
    private readonly LatestWinsUpdateQueue<CardUpdateRequest> _cardUpdateQueue;
    private static readonly AdaptiveCardRenderer _renderer;
    private static bool _customElementsRegistered;
    private ContentFormViewModel? _viewModel;

    // LOAD-BEARING: if you don't hang onto a reference to the RenderedAdaptiveCard
    // then the GC might clean it up sometime, even while the card is in the UI
    // tree. If this gets GC'ed, then it'll revoke our Action handler, and the
    // form will do seemingly nothing.
    private RenderedAdaptiveCard? _renderedCard;
    private AdaptiveCard? _adaptiveCard;
    private IncrementalNodeSnapshot? _cardSnapshot;
    private string? _cardJson;
    private long _cardVersion;
    private long _cardSessionGeneration;

    public ContentFormViewModel? ViewModel { get => _viewModel; set => AttachViewModel(value); }

    static ContentFormControl()
    {
        // We can't use `CardOverrideStyles` here yet, because we haven't called InitializeComponent once.
        // But also, the default value isn't `null` here. It's... some other default empty value.
        // So clear it out so that we know when the first time we get created is
        _renderer = new AdaptiveCardRenderer()
        {
            OverrideStyles = null,
        };
    }

    internal static void RegisterCustomElements()
    {
        if (_customElementsRegistered)
        {
            return;
        }

        Register<AdaptiveStringListInputElement, AdaptiveStringListInputElementParser, AdaptiveStringListInputElementRenderer>();
        Register<AdaptiveFilePathListInputElement, AdaptiveFilePathListInputElementParser, AdaptiveFilePathListInputElementRenderer>();
        Register<AdaptiveKeyValueListInputElement, AdaptiveKeyValueListInputElementParser, AdaptiveKeyValueListInputElementRenderer>();
        Register<AdaptiveFilePathInputElement, AdaptiveFilePathInputElementParser, AdaptiveFilePathInputElementRenderer>();

        _customElementsRegistered = true;

        return;

        static void Register<TElement, TParser, TRenderer>()
            where TElement : IAdaptiveCardElement, ICustomAdaptiveCardElement
            where TParser : IAdaptiveElementParser, new()
            where TRenderer : IAdaptiveElementRenderer, new()
        {
            AdaptiveCardParserRegistrations.ElementParsers.Set(TElement.CustomInputType, new TParser());
            _renderer.ElementRenderers.Set(TElement.CustomInputType, new TRenderer());
        }
    }

    public ContentFormControl()
    {
        this.InitializeComponent();
        var lightTheme = ActualTheme == Microsoft.UI.Xaml.ElementTheme.Light;
        _renderer = new AdaptiveCardRenderer()
        {
            HostConfig = lightTheme ? AdaptiveCardsConfig.Light : AdaptiveCardsConfig.Dark,
            OverrideStyles = CardOverrideStyles,
        };
        _cardUpdateQueue = new LatestWinsUpdateQueue<CardUpdateRequest>(
            DisplayCardAsync,
            ex => Logger.LogError("Unexpected incremental Adaptive Card update failure", ex));

        // TODO in the future, we should handle ActualThemeChanged and replace
        // our rendered card with one for that theme. But today is not that day
    }

    private void AttachViewModel(ContentFormViewModel? vm)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        ResetCard();

        _viewModel = vm;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            var c = _viewModel.Card;
            if (c is not null)
            {
                RequestDisplayCard(c);
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
                RequestDisplayCard(c);
            }
        }
    }

    private void RequestDisplayCard(AdaptiveCardParseResult result)
    {
        _cardUpdateQueue.Enqueue(new CardUpdateRequest(result, _cardSessionGeneration));
    }

    private async Task DisplayCardAsync(CardUpdateRequest request)
    {
        var result = request.Result;
        RenderedAdaptiveCard candidate;
        try
        {
            candidate = _renderer.RenderAdaptiveCard(result.AdaptiveCard);
        }
        catch
        {
            // A transient renderer failure must not blank a previously valid form.
            return;
        }

        if (candidate.FrameworkElement is not FrameworkElement candidateRoot)
        {
            return;
        }

        var candidateSnapshot = IncrementalAdaptiveCardVisualTree.Build(candidateRoot, result.AdaptiveCard);
        var candidateJson = result.AdaptiveCard.ToJson().Stringify();
        if (request.SessionGeneration != _cardSessionGeneration)
        {
            return;
        }

        if (_renderedCard?.FrameworkElement is FrameworkElement currentRoot
            && _cardSnapshot is not null)
        {
            var plan = IncrementalTreeDiffer.CreatePlan(_cardSnapshot, candidateSnapshot, _cardVersion);
            var cardJsonChanged = !string.Equals(_cardJson, candidateJson, StringComparison.Ordinal);
            var canRetainCurrentLease = plan.Disposition == IncrementalPlanDisposition.PatchInPlace
                || (plan.Disposition == IncrementalPlanDisposition.NoChanges && !cardJsonChanged);

            if (canRetainCurrentLease)
            {
                var applied = await IncrementalAdaptiveCardVisualTree.TryApplyAsync(
                    currentRoot,
                    plan,
                    _cardVersion,
                    () => request.SessionGeneration == _cardSessionGeneration);
                if (applied
                    && request.SessionGeneration == _cardSessionGeneration)
                {
                    _cardSnapshot = candidateSnapshot;
                    _cardJson = candidateJson;
                    _adaptiveCard = result.AdaptiveCard;
                    _cardVersion++;
                }

                // Decode or validation failures retain the last good visible card. A newer request
                // waits in the queue's single pending slot rather than cancelling this active update.
                return;
            }
        }

        if (request.SessionGeneration != _cardSessionGeneration)
        {
            return;
        }

        SwapCard(candidate, result.AdaptiveCard, candidateSnapshot, candidateJson, candidateRoot);
    }

    private void SwapCard(
        RenderedAdaptiveCard candidate,
        AdaptiveCard adaptiveCard,
        IncrementalNodeSnapshot snapshot,
        string cardJson,
        FrameworkElement candidateRoot)
    {
        var oldRenderedCard = _renderedCard;
        var oldRoot = oldRenderedCard?.FrameworkElement as FrameworkElement;

        candidateRoot.KeyDown += OnFormKeyDown;
        candidateRoot.Loaded += OnFrameworkElementLoaded;
        candidateRoot.LayoutUpdated += OnFrameworkElementLayoutUpdated;
        candidate.Action += Rendered_Action;

        _renderedCard = candidate;
        _adaptiveCard = adaptiveCard;
        _cardSnapshot = snapshot;
        _cardJson = cardJson;
        _cardVersion++;

        // The candidate is completely rendered before this single assignment. The stable host is never
        // cleared, so unsupported changes cannot produce an intermediate empty frame.
        CardHost.Child = candidateRoot;

        if (oldRoot is not null)
        {
            oldRoot.KeyDown -= OnFormKeyDown;
            oldRoot.Loaded -= OnFrameworkElementLoaded;
            oldRoot.LayoutUpdated -= OnFrameworkElementLayoutUpdated;
        }

        if (oldRenderedCard is not null)
        {
            oldRenderedCard.Action -= Rendered_Action;
        }
    }

    private void ResetCard()
    {
        _cardSessionGeneration++;
        _cardUpdateQueue.ClearPending();

        var oldRenderedCard = _renderedCard;
        if (oldRenderedCard?.FrameworkElement is FrameworkElement oldRoot)
        {
            oldRoot.KeyDown -= OnFormKeyDown;
            oldRoot.Loaded -= OnFrameworkElementLoaded;
            oldRoot.LayoutUpdated -= OnFrameworkElementLayoutUpdated;
        }

        if (oldRenderedCard is not null)
        {
            oldRenderedCard.Action -= Rendered_Action;
        }

        CardHost.Child = null;
        _renderedCard = null;
        _adaptiveCard = null;
        _cardSnapshot = null;
        _cardJson = null;
        _cardVersion++;
    }

    private sealed class CardUpdateRequest(AdaptiveCardParseResult result, long sessionGeneration)
    {
        public AdaptiveCardParseResult Result { get; } = result;

        public long SessionGeneration { get; } = sessionGeneration;
    }

    private void OnFrameworkElementLayoutUpdated(object? sender, object e)
    {
        // Only fix once — unhook from sender (not _renderedCard, which may have been
        // reassigned by the time this fires).
        if (sender is FrameworkElement element)
        {
            element.LayoutUpdated -= OnFrameworkElementLayoutUpdated;
            FixToggleAccessibilityNames(element);
        }
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

    /// <summary>
    /// Fixes missing AutomationProperties.Name on CheckBox and ToggleSwitch controls
    /// rendered by the Adaptive Cards library (AdaptiveCards.Rendering.WinUI3 v2.x).
    /// Without this fix, Narrator announces "space, checkbox, checked" instead of the
    /// actual setting label. This method walks the tree, finds CheckBox/ToggleSwitch
    /// controls missing an automation name, and sets it from the adjacent label TextBlock.
    /// </summary>
    private static void FixToggleAccessibilityNames(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is CheckBox checkBox)
            {
                var existingName = AutomationProperties.GetName(checkBox);
                if (string.IsNullOrEmpty(existingName))
                {
                    // In Adaptive Cards, the label TextBlock is a sibling to the CheckBox's
                    // container within a shared Grid parent. Walk up to find that Grid, then
                    // search for a TextBlock with actual text content.
                    var labelText = FindAdjacentLabel(checkBox);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        AutomationProperties.SetName(checkBox, labelText);
                    }
                }
            }
            else if (child is ToggleSwitch toggleSwitch)
            {
                var existingName = AutomationProperties.GetName(toggleSwitch);
                if (string.IsNullOrEmpty(existingName))
                {
                    var labelText = FindAdjacentLabel(toggleSwitch);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        AutomationProperties.SetName(toggleSwitch, labelText);
                    }
                }
            }

            // Recurse into children
            FixToggleAccessibilityNames(child);
        }
    }

    /// <summary>
    /// Walks up the visual tree from a control to find the nearest parent Grid,
    /// then searches that Grid's descendants for a TextBlock with non-whitespace text.
    /// This handles the Adaptive Cards layout where the label TextBlock is a sibling
    /// of the CheckBox's container within a shared Grid row.
    /// </summary>
    private static string? FindAdjacentLabel(FrameworkElement control)
    {
        // Walk up the tree to find the nearest Grid ancestor (the row container)
        DependencyObject? current = control;
        Grid? parentGrid = null;
        for (var depth = 0; depth < 10 && current != null; depth++)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is Grid grid)
            {
                // Find the grid that contains multiple children (the row container)
                if (VisualTreeHelper.GetChildrenCount(grid) > 1)
                {
                    parentGrid = grid;
                    break;
                }
            }
        }

        if (parentGrid == null)
        {
            return null;
        }

        // Search the parent Grid for a TextBlock with actual text
        return FindFirstNonEmptyTextBlock(parentGrid);
    }

    private static string? FindFirstNonEmptyTextBlock(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                return tb.Text;
            }

            var result = FindFirstNonEmptyTextBlock(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
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

    private void OnFormKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Snapshot the fields so a subsequent DisplayCard call can't swap the
        // rendered/parsed card out from under us mid-method. This keeps the
        // resolved submit action and the gathered inputs from the same card.
        var renderedCard = _renderedCard;
        var adaptiveCard = _adaptiveCard;

        if (e.Key != VirtualKey.Enter || renderedCard == null || adaptiveCard == null)
        {
            return;
        }

        // Only submit when Enter is pressed inside a single-line TextBox
        if (e.OriginalSource is TextBox textBox && !textBox.AcceptsReturn)
        {
            // Find the first Submit or Execute action on the card
            IAdaptiveActionElement? submitAction = null;
            foreach (var action in adaptiveCard.Actions)
            {
                if (action is AdaptiveSubmitAction or AdaptiveExecuteAction)
                {
                    submitAction = action;
                    break;
                }
            }

            if (submitAction != null)
            {
                e.Handled = true;

                // Validate (and gather) the inputs before submitting. AsJson() only
                // returns the values cached by a successful ValidateInputs() call, so
                // skipping this would submit an empty payload. This mirrors what the
                // renderer does internally when a submit button is clicked.
                var inputs = renderedCard.UserInputs;
                if (inputs.ValidateInputs(submitAction))
                {
                    ViewModel?.HandleSubmit(submitAction, inputs.AsJson());
                }
            }
        }
    }

    private void Rendered_Action(RenderedAdaptiveCard sender, AdaptiveActionEventArgs args) =>
        ViewModel?.HandleSubmit(args.Action, args.Inputs.AsJson());
}
