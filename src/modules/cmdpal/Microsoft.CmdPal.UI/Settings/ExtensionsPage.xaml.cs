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

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - Page lifecycle manages disposal
public sealed partial class ExtensionsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;

    public ExtensionsPage()
    {
        this.InitializeComponent();

        var settingsService = App.Current.Services.GetRequiredService<SettingsService>();
        var topLevelCommandManager = App.Current.Services.GetRequiredService<TopLevelCommandManager>();
        var themeService = App.Current.Services.GetRequiredService<IThemeService>();
        viewModel = new SettingsViewModel(settingsService, topLevelCommandManager, _mainTaskScheduler, themeService);
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
}
