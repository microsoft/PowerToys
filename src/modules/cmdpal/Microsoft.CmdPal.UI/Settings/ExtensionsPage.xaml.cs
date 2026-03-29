// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private int _lastFocusedIndex;

    public ExtensionsPage()
    {
        this.InitializeComponent();

        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        viewModel = new SettingsViewModel(topLevelCommandManager, _mainTaskScheduler, themeService, settingsService);
    }

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsCard card)
        {
            if (card.DataContext is ProviderSettingsViewModel vm)
            {
                WeakReferenceMessenger.Default.Send<NavigateToExtensionSettingsMessage>(new(vm));
            }
        }
    }

    private void OnFindInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox?.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private async void MenuFlyoutItem_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await FallbackRankerDialog!.ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error when showing FallbackRankerDialog", ex);
        }
    }

    private void SettingsCard_GotFocus(object sender, RoutedEventArgs e)
    {
        // Track focus whenever any part of the card gets focus (including children like ToggleSwitch)
        if (sender is SettingsCard card && viewModel is not null)
        {
            var dataContext = card.DataContext as ProviderSettingsViewModel;
            if (dataContext is not null)
            {
                var filteredProviders = viewModel.Extensions.FilteredProviders;
                var index = filteredProviders.IndexOf(dataContext);
                if (index >= 0)
                {
                    _lastFocusedIndex = index;
                }
            }
        }
    }

    private void ProvidersRepeater_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (viewModel is null || ProvidersRepeater is null)
        {
            return;
        }

        // Only intervene when focus is coming into the ItemsRepeater from outside
        if (args.OldFocusedElement != null && IsElementInsideRepeater(args.OldFocusedElement))
        {
            return;
        }

        var filteredProviders = viewModel.Extensions.FilteredProviders;

        if (filteredProviders.Count == 0)
        {
            return;
        }

        // Get the last focused index, defaulting to 0
        var index = _lastFocusedIndex;
        if (index < 0 || index >= filteredProviders.Count)
        {
            index = 0;
        }

        // Check if WinUI is trying to focus something other than our target
        var shouldIntervene = false;

        // If direction is Previous (Shift+Tab), we need to intervene
        if (args.Direction == FocusNavigationDirection.Previous || args.Direction == FocusNavigationDirection.Up)
        {
            shouldIntervene = true;
        }

        // Also intervene if the NewFocusedElement is not at our target index
        else if (args.NewFocusedElement is DependencyObject newFocus)
        {
            // Check if the new focus element is inside our target card
            var targetCard = ProvidersRepeater.TryGetElement(index) as SettingsCard;
            if (targetCard != null && !IsElementInsideCard(newFocus, targetCard))
            {
                shouldIntervene = true;
            }
        }

        if (shouldIntervene)
        {
            // Ensure the target element is realized before trying to focus it
            ProvidersRepeater.GetOrCreateElement(index);

            // Get the target card
            var targetCard = ProvidersRepeater.TryGetElement(index) as SettingsCard;

            if (targetCard != null)
            {
                // For shift-tab or wrong target, cancel and manually set focus
                args.TryCancel();
                args.Handled = true;

                // Set focus asynchronously to the target card and scroll it into view
                _ = targetCard.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    targetCard.Focus(FocusState.Keyboard);
                    BringCardIntoView(targetCard);
                });
            }
        }
        else
        {
            // For normal Tab forward, just redirect
            ProvidersRepeater.GetOrCreateElement(index);
            var targetCard = ProvidersRepeater.TryGetElement(index) as SettingsCard;

            if (targetCard != null)
            {
                args.TrySetNewFocusedElement(targetCard);
                args.Handled = true;

                // Set focus asynchronously to the target card and scroll it into view
                _ = targetCard.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    BringCardIntoView(targetCard);
                });
            }
        }
    }

    private void BringCardIntoView(SettingsCard card)
    {
        card.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.5, // Center vertically
        });
    }

    private bool IsElementInsideCard(DependencyObject element, SettingsCard card)
    {
        var parent = element;
        while (parent != null)
        {
            if (ReferenceEquals(parent, card))
            {
                return true;
            }

            parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
        }

        return false;
    }

    private bool IsElementInsideRepeater(object element)
    {
        if (element is not DependencyObject depObj)
        {
            return false;
        }

        var parent = depObj;
        while (parent != null)
        {
            if (ReferenceEquals(parent, ProvidersRepeater))
            {
                return true;
            }

            parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
        }

        return false;
    }
}
