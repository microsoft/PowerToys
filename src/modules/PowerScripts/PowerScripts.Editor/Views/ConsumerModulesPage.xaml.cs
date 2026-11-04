// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class ConsumerModulesPage : Page
{
    private PowerScriptsViewModel ViewModel { get; set; }

    public ConsumerModulesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                await ViewModel.RefreshConsumersAsync();
                ConsumerError.IsOpen = false;
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel
            ?? throw new ArgumentException("Consumer navigation requires the PowerScripts view model.", nameof(e));
        Bindings.Update();
    }

    private async void ConsumerEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { Tag: string id } toggle || !ViewModel.AreConsumerSettingsReady)
        {
            return;
        }

        var consumer = id == "advanced-paste" ? ViewModel.AdvancedPasteConsumer : ViewModel.KeyboardManagerConsumer;
        if (!consumer.CanChange || toggle.IsOn == consumer.IsEnabled)
        {
            return;
        }

        try
        {
            await ViewModel.SetConsumerEnabledAsync(consumer, toggle.IsOn);
            ConsumerError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.LogError("Unable to update PowerScripts consumer settings.", exception);
        ConsumerError.Message = exception.Message;
        ConsumerError.IsOpen = true;
    }
}
