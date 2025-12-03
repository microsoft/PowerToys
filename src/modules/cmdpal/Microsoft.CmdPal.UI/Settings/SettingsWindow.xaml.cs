// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;
using TitleBar = Microsoft.UI.Xaml.Controls.TitleBar;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class SettingsWindow : WindowEx,
    IDisposable,
    IRecipient<NavigateToExtensionSettingsMessage>,
    IRecipient<QuitMessage>
{
    private readonly LocalKeyboardListener _localKeyboardListener;

    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    // Gets or sets optional action invoked after NavigationView is loaded.
    public Action NavigationViewLoaded { get; set; } = () => { };

    public SettingsWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        var title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.Title = title;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.AppTitleBar.Title = title;
        PositionCentered();

        WeakReferenceMessenger.Default.Register<NavigateToExtensionSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        _localKeyboardListener = new LocalKeyboardListener();
        _localKeyboardListener.KeyPressed += LocalKeyboardListener_OnKeyPressed;
        _localKeyboardListener.Start();
        Closed += SettingsWindow_Closed;
        RootElement.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootElement_OnPointerPressed), true);
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    // Handles NavigationView loaded event.
    // Sets up initial navigation and accessibility notifications.
    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Delay necessary to ensure NavigationView visual state can match navigation
        Task.Delay(500).ContinueWith(_ => this.NavigationViewLoaded?.Invoke(), TaskScheduler.FromCurrentSynchronizationContext());

        NavView.SelectedItem = NavView.MenuItems[0];
        Navigate("General");

        if (sender is NavigationView navigationView)
        {
            // Register for pane open/close changes to announce to screen readers
            navigationView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, AnnounceNavigationPaneStateChanged);
        }
    }

    // Announces navigation pane open/close state to screen readers for accessibility.
    private void AnnounceNavigationPaneStateChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is NavigationView navigationView)
        {
            UIHelper.AnnounceActionForAccessibility(
            ue: (UIElement)sender,
            (sender as NavigationView)?.IsPaneOpen == true ? RS_.GetString("NavigationPaneOpened") : RS_.GetString("NavigationPaneClosed"),
            "NavigationViewPaneIsOpenChangeNotificationId");
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var selectedItem = args.InvokedItemContainer;
        Navigate((selectedItem.Tag as string)!);
    }

    private void Navigate(string page)
    {
        var pageType = page switch
        {
            "General" => typeof(GeneralPage),
            "Appearance" => typeof(AppearancePage),
            "Extensions" => typeof(ExtensionsPage),
            _ => null,
        };
        if (pageType is not null)
        {
            NavFrame.Navigate(pageType);
        }
    }

    private void Navigate(ProviderSettingsViewModel extension)
    {
        NavFrame.Navigate(typeof(ExtensionPage), extension);
    }

    private void PositionCentered()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;
            AppWindow.Move(centeredPosition);
        }
    }

    public void Receive(NavigateToExtensionSettingsMessage message) => Navigate(message.ProviderSettingsVM);

    private void NavigationBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is Crumb crumb)
        {
            if (crumb.Data is string data)
            {
                if (!string.IsNullOrEmpty(data))
                {
                    Navigate(data);
                }
            }
        }
    }

    private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        WeakReferenceMessenger.Default.Send<Microsoft.UI.Xaml.WindowActivatedEventArgs>(args);
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        WeakReferenceMessenger.Default.Send<SettingsWindowClosedMessage>();

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        if (args.DisplayMode is NavigationViewDisplayMode.Compact or NavigationViewDisplayMode.Minimal)
        {
            AppTitleBar.IsPaneToggleButtonVisible = true;
            WorkAroundIcon.Margin = new Thickness(8, 0, 16, 0); // Required for workaround, see XAML comment
        }
        else
        {
            AppTitleBar.IsPaneToggleButtonVisible = false;
            WorkAroundIcon.Margin = new Thickness(16, 0, 8, 0); // Required for workaround, see XAML comment
        }
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());
    }

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TryGoBack()
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        TryGoBack();
    }

    private void LocalKeyboardListener_OnKeyPressed(object? sender, LocalKeyboardListenerKeyPressedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.GoBack:
            case VirtualKey.XButton1:
                TryGoBack();
                break;

            case VirtualKey.Left:
                var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                if (altPressed)
                {
                    TryGoBack();
                }

                break;
        }
    }

    private void RootElement_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var ptrPt = e.GetCurrentPoint(RootElement);
                if (ptrPt.Properties.IsXButton1Pressed)
                {
                    TryGoBack();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error handling mouse button press event", ex);
        }
    }

    public void Dispose()
    {
        _localKeyboardListener?.Dispose();
    }

    private void NavFrame_OnNavigated(object sender, NavigationEventArgs e)
    {
        BreadCrumbs.Clear();

        if (e.SourcePageType == typeof(GeneralPage))
        {
            NavView.SelectedItem = GeneralPageNavItem;
            var pageType = RS_.GetString("Settings_PageTitles_GeneralPage");
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else if (e.SourcePageType == typeof(AppearancePage))
        {
            NavView.SelectedItem = AppearancePageNavItem;
            var pageType = RS_.GetString("Settings_PageTitles_AppearancePage");
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else if (e.SourcePageType == typeof(ExtensionsPage))
        {
            NavView.SelectedItem = ExtensionPageNavItem;
            var pageType = RS_.GetString("Settings_PageTitles_ExtensionsPage");
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else if (e.SourcePageType == typeof(ExtensionPage) && e.Parameter is ProviderSettingsViewModel vm)
        {
            NavView.SelectedItem = ExtensionPageNavItem;
            var extensionsPageType = RS_.GetString("Settings_PageTitles_ExtensionsPage");
            BreadCrumbs.Add(new(extensionsPageType, extensionsPageType));
            BreadCrumbs.Add(new(vm.DisplayName, vm));
        }
        else
        {
            BreadCrumbs.Add(new($"[{e.SourcePageType?.Name}]", string.Empty));
            Logger.LogError($"Unknown breadcrumb for page type '{e.SourcePageType}'");
        }
    }
}

public readonly struct Crumb
{
    public Crumb(string label, object data)
    {
        Label = label;
        Data = data;
    }

    public string Label { get; }

    public object Data { get; }

    public override string ToString() => Label;
}
