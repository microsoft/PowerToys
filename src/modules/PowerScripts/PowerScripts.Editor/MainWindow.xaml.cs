// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Editor.Helpers;
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
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(EditorTitleBar);
        AppWindow.Resize(new SizeInt32(1400, 920));
        foreach (var frame in _frames)
        {
            frame.Navigated += (_, _) => UpdateBackButton();
        }

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
        UpdateBackButton();
    }

    internal void UpdateBackButton()
    {
        EditorTitleBar.IsBackButtonEnabled = TabContent.Content is Frame { CanGoBack: true } frame &&
            frame.Content is not PowerScriptTemplatesPage { IsCreating: true };
        EditorSearch.IsEnabled = _frames[0].Content is not PowerScriptTemplatesPage;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (TabContent.Content is Frame { CanGoBack: true } frame)
        {
            EditorNavigation.GoBack(frame);
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args) => Navigation.IsPaneOpen = !Navigation.IsPaneOpen;

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && TabContent is not null &&
            _frames[0].Content is not PowerScriptTemplatesPage)
        {
            Navigation.SelectedItem = MyScriptsTab;
            if (_frames[0].Content is not MyScriptsPage)
            {
                _frames[0].Navigate(typeof(MyScriptsPage), _viewModel);
            }
        }
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer == GlobalSettingsItem)
        {
            try
            {
                EditorNavigation.OpenGlobalSettings();
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to open PowerScripts Settings.", exception);
                ShellError.Message = exception.Message;
                ShellError.IsOpen = true;
            }
        }
    }
}
