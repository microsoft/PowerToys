// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Views;

public sealed partial class PowerScriptsActivityPage : Page
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isActive;

    private PowerScriptsViewModel ViewModel { get; set; }

    public PowerScriptsActivityPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        _refreshTimer.Tick += async (_, _) => await ViewModel.RefreshActivityAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = e.Parameter as PowerScriptsViewModel ??
            throw new ArgumentException("Activity navigation requires the PowerScripts view model.", nameof(e));
        Bindings.Update();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isActive = true;
        await ViewModel.RefreshActivityAsync();
        if (_isActive)
        {
            _refreshTimer.Start();
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _isActive = false;
        _refreshTimer.Stop();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshActivityAsync();

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearTitle"),
                Content = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearHelp"),
                PrimaryButtonText = PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityClearConfirm"),
                CloseButtonText = PowerScriptsViewModel.GetResourceString("PowerScripts_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.RefreshActivityAsync(clearCompleted: true);
            }
        }
        finally
        {
            if (_isActive)
            {
                _refreshTimer.Start();
            }
        }
    }
}
