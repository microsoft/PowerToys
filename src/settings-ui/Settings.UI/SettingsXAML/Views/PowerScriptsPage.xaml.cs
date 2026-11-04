// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class PowerScriptsPage : NavigablePage, IRefreshablePage
{
    private PowerScriptsViewModel ViewModel { get; }

    public PowerScriptsPage()
    {
        ViewModel = new PowerScriptsViewModel(
            SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default),
            ShellPage.SendDefaultIPCMessage);
        InitializeComponent();
        Loaded += (_, _) => RefreshEnabledState();
    }

    public async void RefreshEnabledState()
    {
        try
        {
            await ViewModel.RefreshConsumersAsync();
            EditorError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void PowerScriptsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && ViewModel.CanChangePowerScriptsEnabled && toggle.IsOn != ViewModel.IsEnabled)
        {
            try
            {
                ViewModel.IsEnabled = toggle.IsOn;
                await ViewModel.RefreshConsumersAsync();
                EditorError.IsOpen = false;
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "PowerToys.PowerScriptsEditor.exe");
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false })
                ?? throw new InvalidOperationException("Unable to start the PowerScripts editor.");
            EditorError.IsOpen = false;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception)
    {
        Logger.LogError("Unable to open or configure PowerScripts.", exception);
        EditorError.Message = exception.Message;
        EditorError.IsOpen = true;
    }
}
