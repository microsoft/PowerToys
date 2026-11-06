// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Editor.Views;
using PowerScripts.UI.ViewModels;
using Windows.Graphics;

namespace PowerScripts.Editor;

public sealed partial class MainWindow : Window
{
    private readonly Frame[] _frames = { new(), new(), new() };
    private readonly Type[] _pages = { typeof(MyScriptsPage), typeof(PowerScriptsActivityPage), typeof(ConsumerModulesPage) };
    private readonly PowerScriptsViewModel _viewModel;

    public MainWindow()
    {
        var settings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default);
        _viewModel = new PowerScriptsViewModel(settings, null);
        InitializeComponent();
        Root.RequestedTheme = settings.SettingsConfig.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        AppWindow.Resize(new SizeInt32(1100, 800));
        Navigation.SelectedItem = MyScriptsTab;
        Activated += async (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated && _frames[0].Content is MyScriptsPage page)
            {
                await page.RefreshConfigurationAsync();
            }

            if (args.WindowActivationState != WindowActivationState.Deactivated && ReferenceEquals(TabContent.Content, _frames[2]) && _frames[2].Content is ConsumerModulesPage consumers)
            {
                await consumers.RefreshConsumersAsync();
            }
        };
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        var index = int.Parse(tag, CultureInfo.InvariantCulture);
        var frame = _frames[index];
        if (frame.Content is null && !frame.Navigate(_pages[index], _viewModel))
        {
            throw new InvalidOperationException("Unable to open the PowerScripts editor page.");
        }

        TabContent.Content = frame;
    }
}
