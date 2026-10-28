// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class TextExpanderPage : NavigablePage, IRefreshablePage
{
    public TextExpanderPage()
    {
        var settingsUtils = new TextExpanderSettingsUtils();
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
}
