// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class ConsumerModulesPage : Page
{
    private bool _refreshing;

    private PowerScriptsViewModel ViewModel { get; set; }

    public ConsumerModulesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshConsumersAsync();
    }

    public async Task RefreshConsumersAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            await ViewModel.RefreshConsumersAsync();
            await ViewModel.RefreshModuleScriptsAsync();
            ConsumerError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            _refreshing = false;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel
            ?? throw new ArgumentException("Consumer navigation requires the PowerScripts view model.", nameof(e));
        Bindings.Update();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Bindings.StopTracking();
        base.OnNavigatedFrom(e);
    }

    private async void ConsumerEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch { Tag: string id } toggle || _refreshing || !ViewModel.AreModuleCataloguesReady)
        {
            return;
        }

        var consumer = id switch
        {
            "advanced-paste" => ViewModel.AdvancedPasteConsumer,
            "keyboard-manager" => ViewModel.KeyboardManagerConsumer,
            "file-explorer" => ViewModel.FileExplorerConsumer,
            "command-palette" => ViewModel.CommandPaletteConsumer,
            _ => throw new InvalidOperationException("Unknown PowerScripts consumer."),
        };
        if (!consumer.CanChange || toggle.IsOn == consumer.IsEnabled)
        {
            return;
        }

        _refreshing = true;
        try
        {
            await ViewModel.SetConsumerEnabledAsync(consumer, toggle.IsOn);
            await ViewModel.RefreshModuleScriptsAsync();
            ConsumerError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            _refreshing = false;
        }
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private void OpenScript_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PowerScriptListItem script })
        {
            try
            {
                if (!Frame.Navigate(typeof(PowerScriptSettingsPage), new PowerScriptSettingsPage.NavigationContext(ViewModel, script)))
                {
                    throw new InvalidOperationException(PowerScriptsViewModel.GetResourceString("PowerScripts_SettingsNavigationFailed"));
                }
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.LogError("Unable to manage PowerScripts modules.", exception);
        ConsumerError.Message = exception.Message;
        ConsumerError.IsOpen = true;
    }
}
