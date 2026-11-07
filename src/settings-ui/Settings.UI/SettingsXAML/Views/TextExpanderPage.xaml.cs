// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class TextExpanderPage : NavigablePage, IRefreshablePage
{
    private readonly string _settingsFile;

    public TextExpanderPage()
    {
        var settingsUtils = new TextExpanderSettingsUtils();
        _settingsFile = settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
        ViewModel = new TextExpanderViewModel(
            settingsUtils,
            SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
            ShellPage.SendDefaultIPCMessage,
            App.IsElevated);
        InitializeComponent();
        Loaded += (_, _) => ViewModel.RefreshEnabledState();
    }

    public TextExpanderViewModel ViewModel { get; }

    public void RefreshEnabledState() => ViewModel.RefreshEnabledState();

    private void ManageSnippets_Click(object sender, RoutedEventArgs e) => ViewModel.LaunchEditor();

    // Selects settings.json in File Explorer, so the file the error bar is about is easy to find and fix.
    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_settingsFile}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Logger.LogError("Could not open the Text Expander settings folder.", ex);
        }
    }
}
