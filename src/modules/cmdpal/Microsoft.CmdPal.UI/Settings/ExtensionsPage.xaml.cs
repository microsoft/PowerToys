// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Settings;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - Page lifecycle manages disposal
public sealed partial class ExtensionsPage : Page
{
    private readonly ILogger _logger;
    private readonly FallbackRankerDialog _fallbackRankerDialog;

    private readonly SettingsViewModel? viewModel;

    public ExtensionsPage(
        SettingsViewModel settingsViewModel,
        FallbackRankerDialog fallbackRankerDialog,
        ILogger logger)
    {
        this.InitializeComponent();
        _logger = logger;
        viewModel = settingsViewModel;
        _fallbackRankerDialog = fallbackRankerDialog;

        FallbackRankerContainer.Content = _fallbackRankerDialog;
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
            await _fallbackRankerDialog!.ShowAsync();
        }
        catch (Exception ex)
        {
            Log_FallbackRankerDialogError(ex);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error when showing FallbackRankerDialog")]
    partial void Log_FallbackRankerDialogError(Exception ex);
}
