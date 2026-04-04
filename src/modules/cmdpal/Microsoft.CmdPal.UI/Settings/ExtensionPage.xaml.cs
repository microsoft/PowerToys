// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionPage : Page
{
    private const int FocusRetryDelayMs = 50;
    private const int MaxFocusAttempts = 10;
    private const int MaxSettingsFocusAttempts = 40;
    private static readonly BringIntoViewOptions BringIntoViewTopOptions = new()
    {
        AnimationDesired = false,
        VerticalAlignmentRatio = 0.0,
    };

    private ExtensionSettingsNavigationRequest? _extensionSettingsRequest;

    public ProviderSettingsViewModel? ViewModel { get; private set; }

    public ExtensionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _extensionSettingsRequest = e.Parameter is ExtensionSettingsNavigationRequest request
            ? request
            : e.Parameter is ProviderSettingsViewModel vm
                ? new ExtensionSettingsNavigationRequest(vm)
                : throw new ArgumentException($"{nameof(ExtensionPage)} navigation args should be passed a {nameof(ProviderSettingsViewModel)} or {nameof(ExtensionSettingsNavigationRequest)}");

        ViewModel = _extensionSettingsRequest.ProviderSettingsViewModel;

        Loaded -= ExtensionPage_Loaded;
        Loaded += ExtensionPage_Loaded;
    }

    private async void ExtensionPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ExtensionPage_Loaded;
        await TryFocusRequestedContextAsync();
    }

    private async Task TryFocusRequestedContextAsync()
    {
        try
        {
            if (_extensionSettingsRequest is null ||
                ViewModel is null ||
                _extensionSettingsRequest.FocusTarget == ExtensionSettingsFocusTarget.None ||
                !ViewModel.IsEnabled)
            {
                return;
            }

            if (_extensionSettingsRequest.FocusTarget == ExtensionSettingsFocusTarget.SettingsPage)
            {
                await TryFocusSettingsPageAsync();
                return;
            }

            if (string.IsNullOrEmpty(_extensionSettingsRequest.CommandId))
            {
                return;
            }

            var commandIndex = ViewModel.TopLevelCommands.FindIndex(command => command.Id == _extensionSettingsRequest.CommandId);
            if (commandIndex < 0)
            {
                return;
            }

            var requestedCommand = ViewModel.TopLevelCommands[commandIndex];

            for (var attempt = 0; attempt < MaxFocusAttempts; attempt++)
            {
                UpdateLayout();
                RootScrollViewer.UpdateLayout();
                TopLevelCommandsRepeater.UpdateLayout();

                if (TopLevelCommandsRepeater.GetOrCreateElement(commandIndex) is not FrameworkElement commandElement)
                {
                    await Task.Delay(FocusRetryDelayMs);
                    continue;
                }

                if (!IsRequestedCommandElement(commandElement, requestedCommand))
                {
                    await Task.Delay(FocusRetryDelayMs);
                    continue;
                }

                var scrollTarget = commandElement;
                if (commandElement is SettingsExpander expander)
                {
                    expander.IsExpanded = true;
                    scrollTarget = expander;
                    BringElementIntoViewAtTop(expander);
                    commandElement.UpdateLayout();
                }

                var focusTarget = FindFocusTarget(commandElement, _extensionSettingsRequest.FocusTarget);
                if (focusTarget is null)
                {
                    await Task.Delay(FocusRetryDelayMs);
                    continue;
                }

                if (focusTarget.Focus(FocusState.Programmatic))
                {
                    BringElementIntoViewAtTop(scrollTarget);

                    if (focusTarget is TextBox textBox)
                    {
                        textBox.SelectAll();
                    }

                    ClearPendingFocusRequest();
                    return;
                }

                await Task.Delay(FocusRetryDelayMs);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to focus the requested extension settings context", ex);
        }
    }

    private async Task TryFocusSettingsPageAsync()
    {
        if (ViewModel is null)
        {
            return;
        }

        for (var attempt = 0; attempt < MaxSettingsFocusAttempts; attempt++)
        {
            // Access SettingsPage to trigger lazy loading of the settings page content.
            _ = ViewModel.SettingsPage;

            UpdateLayout();
            RootScrollViewer.UpdateLayout();
            SettingsFrame.UpdateLayout();

            var focusTarget = FindSettingsFocusTarget();
            if (focusTarget is null)
            {
                if (!ViewModel.LoadingSettings && ViewModel.SettingsPage is null)
                {
                    return;
                }

                await Task.Delay(FocusRetryDelayMs);
                continue;
            }

            BringElementIntoViewAtTop(focusTarget);
            if (focusTarget.Focus(FocusState.Programmatic))
            {
                ClearPendingFocusRequest();
                return;
            }

            await Task.Delay(FocusRetryDelayMs);
        }
    }

    // BEAR LOADING: These control names are coupled to the XAML templates in ExtensionPage.xaml
    // and ShortcutControl. If those templates are refactored (e.g., controls renamed),
    // these lookups will silently fail (focus just won't work). In particular,
    // "EditButton" reaches into ShortcutControl's internal template.
    private static FrameworkElement? FindFocusTarget(FrameworkElement commandElement, ExtensionSettingsFocusTarget focusTarget)
    {
        return focusTarget switch
        {
            ExtensionSettingsFocusTarget.Alias => commandElement.FindDescendant<TextBox>(textBox => textBox.Name == "AliasTextBox"),
            ExtensionSettingsFocusTarget.GlobalHotkey => commandElement
                .FindDescendant<ShortcutControl>(shortcutControl => shortcutControl.Name == "GlobalHotkeyShortcutControl")
                ?.FindDescendant<Button>(button => button.Name == "EditButton"),
            _ => null,
        };
    }

    private static bool IsRequestedCommandElement(FrameworkElement commandElement, TopLevelViewModel requestedCommand)
    {
        if (ReferenceEquals(commandElement.DataContext, requestedCommand))
        {
            return true;
        }

        return commandElement.DataContext is TopLevelViewModel realizedCommand &&
               realizedCommand.Id == requestedCommand.Id;
    }

    private FrameworkElement? FindSettingsFocusTarget()
    {
        BringElementIntoViewAtTop(ExtensionSettingsHeaderTextBlock);
        BringElementIntoViewAtTop(SettingsFrame);

        return SettingsFrame.FindDescendant<Control>(control =>
            control.Visibility == Visibility.Visible &&
            control.IsEnabled &&
            control.IsTabStop);
    }

    private static void BringElementIntoViewAtTop(FrameworkElement element)
    {
        element.StartBringIntoView(BringIntoViewTopOptions);
    }

    private void ClearPendingFocusRequest()
    {
        if (_extensionSettingsRequest is null)
        {
            return;
        }

        _extensionSettingsRequest = _extensionSettingsRequest with
        {
            CommandId = null,
            FocusTarget = ExtensionSettingsFocusTarget.None,
        };
    }

    private async void RankButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await FallbackRankerDialog.ShowAsync();
    }
}
