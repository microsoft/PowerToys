// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private readonly Dictionary<string, WeakReference<SettingsCard>> _vmToCardMap = new();

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

    private void SettingsCard_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        // Store the card reference keyed by Id (not the VM itself) to avoid leaking VM references
        if (sender is SettingsCard card && card.DataContext is ProviderSettingsViewModel newVm)
        {
            _vmToCardMap[newVm.Id] = new WeakReference<SettingsCard>(card);
            newVm.PropertyChanged += ProviderViewModel_PropertyChanged;

            // Immediately update automation name in case DisplayName is already available
            if (card.Content is ToggleSwitch toggle && !string.IsNullOrEmpty(newVm.DisplayName))
            {
                AutomationProperties.SetName(toggle, newVm.DisplayName);
            }
        }
    }

    private void ProviderViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When DisplayName changes, update the ToggleSwitch's automation name
        if (e.PropertyName == nameof(ProviderSettingsViewModel.DisplayName) && sender is ProviderSettingsViewModel vm && !string.IsNullOrEmpty(vm.DisplayName))
        {
            // Get the card reference from our map
            if (_vmToCardMap.TryGetValue(vm.Id, out var cardRef) && cardRef.TryGetTarget(out var card))
            {
                if (card.Content is ToggleSwitch toggle)
                {
                    AutomationProperties.SetName(toggle, vm.DisplayName);
                }
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
}
