// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private WeakReference<SettingsCard>? _lastFocusedCard;
    private bool _isRestoringFocus;

    public ExtensionsPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);
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
            await FallbackRankerDialog!.ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error when showing FallbackRankerDialog", ex);
        }
    }

    private void SettingsCard_GotFocus(object sender, RoutedEventArgs e)
    {
        // Track when a SettingsCard gets focus (not its children like ToggleSwitch)
        if (sender is SettingsCard card && ReferenceEquals(e.OriginalSource, card))
        {
            _lastFocusedCard = new WeakReference<SettingsCard>(card);
        }
    }

    private void ProvidersRepeater_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        // Prevent recursive focus handling
        if (_isRestoringFocus)
        {
            return;
        }

        // Only intervene when focus is coming into the ItemsRepeater from outside
        if (args.OldFocusedElement != null && IsElementInsideRepeater(args.OldFocusedElement))
        {
            return;
        }

        SettingsCard? targetCard = null;

        // Try to restore focus to the last focused card
        if (_lastFocusedCard?.TryGetTarget(out var lastCard) == true && IsCardValid(lastCard))
        {
            targetCard = lastCard;
        }

        // If no valid last focused card, focus the first one
        if (targetCard == null)
        {
            targetCard = GetFirstSettingsCard();
        }

        // Use async focus restoration to avoid crashes during focus transition
        if (targetCard != null)
        {
            args.TryCancel();
            _ = RestoreFocusAsync(targetCard);
        }
    }

    private async Task RestoreFocusAsync(SettingsCard card)
    {
        if (_isRestoringFocus)
        {
            return;
        }

        try
        {
            _isRestoringFocus = true;

            // Verify the card is still valid before focusing
            if (IsCardValid(card))
            {
                // Bring the element into view before focusing
                card.StartBringIntoView(new BringIntoViewOptions
                {
                    AnimationDesired = true,
                    VerticalAlignmentRatio = 0.5, // Center vertically
                });

                _ = card.Focus(FocusState.Keyboard);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error restoring focus to SettingsCard", ex);
        }
        finally
        {
            _isRestoringFocus = false;
        }
    }

    private bool IsElementInsideRepeater(object element)
    {
        if (element is not DependencyObject depObj)
        {
            return false;
        }

        var parent = depObj;
        while (parent != null)
        {
            if (ReferenceEquals(parent, ProvidersRepeater))
            {
                return true;
            }

            parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
        }

        return false;
    }

    private bool IsCardValid(SettingsCard card)
    {
        try
        {
            // Check if the card is in the visual tree and can receive focus
            return IsElementInsideRepeater(card) && card.IsLoaded;
        }
        catch
        {
            return false;
        }
    }

    private SettingsCard? GetFirstSettingsCard()
    {
        try
        {
            if (ProvidersRepeater.TryGetElement(0) is FrameworkElement firstElement)
            {
                return firstElement as SettingsCard;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error getting first SettingsCard", ex);
        }

        return null;
    }
}
