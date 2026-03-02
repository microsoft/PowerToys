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

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);
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

        // Try to get the card at the saved index
        var targetCard = ProvidersRepeater.TryGetElement(index) as SettingsCard;

        if (targetCard != null)
        {
            // Use TrySetNewFocusedElement instead of canceling and deferring
            args.TrySetNewFocusedElement(targetCard);
            args.Handled = true;
        }
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
