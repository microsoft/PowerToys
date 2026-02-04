// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private readonly ILogger _logger;

    private readonly SettingsWindowViewModel viewModel;

    // Gets or sets optional action invoked after NavigationView is loaded.
    public Action NavigationViewLoaded { get; set; } = () => { };

    public SettingsWindow(
        SettingsWindowViewModel viewModel,
        LocalKeyboardListener localKeyboardListener,
        ILogger logger)
    {
        this.InitializeComponent();

        this.viewModel = viewModel;
        _logger = logger;

        this.viewModel.PropertyChanged += ViewModel_PropertyChanged;

        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        var title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.Title = title;
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.AppTitleBar.Title = title;
        PositionCentered();

        WeakReferenceMessenger.Default.Register<NavigateToExtensionSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        _localKeyboardListener = localKeyboardListener;
        _localKeyboardListener.KeyPressed += LocalKeyboardListener_OnKeyPressed;
        _localKeyboardListener.Start();

        Closed += SettingsWindow_Closed;
        RootElement.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootElement_OnPointerPressed), true);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsWindowViewModel.BreadCrumbs))
        {
            NavigationBreadcrumbBar.ItemsSource = viewModel.BreadCrumbs;
        }
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
        viewModel.Navigate((selectedItem.Tag as string)!);
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

    public void Receive(NavigateToExtensionSettingsMessage message) => viewModel.Navigate(message.ProviderSettingsVM);

    private void NavigationBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is Crumb crumb)
        {
            if (crumb.Data is string data)
            {
                if (!string.IsNullOrEmpty(data))
                {
                    viewModel.Navigate(data);
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

    private void TryGoBack() => viewModel.TryGoBack();

    private void TitleBar_BackRequested(TitleBar sender, object args) => TryGoBack();

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
            Log_ErrorHandlingMouseButtonPress(ex);
        }
    }

    public void Dispose()
    {
        _localKeyboardListener?.Dispose();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error handling mouse button press event")]
    partial void Log_ErrorHandlingMouseButtonPress(Exception ex);
}
