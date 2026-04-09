// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;
using TitleBar = Microsoft.UI.Xaml.Controls.TitleBar;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class SettingsWindow : WindowEx,
    IDisposable,
    IRecipient<NavigateToExtensionSettingsMessage>,
    IRecipient<OpenExtensionGalleryScreenshotViewerMessage>,
    IRecipient<QuitMessage>
{
    private readonly LocalKeyboardListener _localKeyboardListener;

    private readonly NavigationViewItem? _internalNavItem;

    private Storyboard? _breadcrumbStoryboard;
    private IReadOnlyList<ExtensionGalleryScreenshotViewModel> _currentScreenshotSet = [];
    private ExtensionGalleryScreenshotViewModel? _currentScreenshot;
    private ImageSource? _currentScreenshotViewerSource;

    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    // Gets or sets optional action invoked after NavigationView is loaded.
    public Action? NavigationViewLoaded { get; set; }

    public Visibility ScreenshotViewerNavigationVisibility =>
        _currentScreenshotSet.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource? CurrentScreenshotViewerSource => _currentScreenshotViewerSource;

    public string CurrentScreenshotDisplayName => _currentScreenshot?.DisplayName ?? string.Empty;

    public string CurrentScreenshotPositionText =>
        _currentScreenshot is null || _currentScreenshotSet.Count == 0
            ? string.Empty
            : $"{GetCurrentScreenshotIndex() + 1} / {_currentScreenshotSet.Count}";

    public SettingsWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        var title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.Title = title;
        this.AppTitleBar.Title = title;
        PositionCentered();

        WeakReferenceMessenger.Default.Register<NavigateToExtensionSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenExtensionGalleryScreenshotViewerMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        _localKeyboardListener = new LocalKeyboardListener();
        _localKeyboardListener.KeyPressed += LocalKeyboardListener_OnKeyPressed;
        _localKeyboardListener.Start();
        Closed += SettingsWindow_Closed;
        RootElement.SizeChanged += RootElement_SizeChanged;
        RootElement.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootElement_OnPointerPressed), true);

        if (!BuildInfo.IsCiBuild)
        {
            _internalNavItem = new NavigationViewItem
            {
                Content = "Internal Tools",
                Icon = new FontIcon { Glyph = "\uEC7A" },
                Tag = "Internal",
            };
            NavView.FooterMenuItems.Add(_internalNavItem);
        }
        else
        {
            _internalNavItem = null;
        }

        Navigate("General");
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

    internal void Navigate(string page)
    {
        Type? pageType;
        switch (page)
        {
            case "General":
                pageType = typeof(GeneralPage);
                break;
            case "Appearance":
                pageType = typeof(AppearancePage);
                break;
            case "Extensions":
                pageType = typeof(ExtensionsPage);
                break;
            case "Gallery":
                pageType = typeof(ExtensionGalleryPage);
                break;
            case "Dock":
                pageType = typeof(DockSettingsPage);
                break;
            case "Internal":
                pageType = typeof(InternalPage);
                break;
            case "":
                // intentional no-op: empty tag means no navigation
                pageType = null;
                break;
            default:
                // unknown page, no-op and log
                pageType = null;
                Logger.LogError($"Unknown settings page tag '{page}'");
                break;
        }

        if (pageType is not null)
        {
            NavFrame.Navigate(pageType);

            // Now, make sure to actually select the correct menu item too
            foreach (var obj in NavView.MenuItems)
            {
                if (obj is NavigationViewItem item)
                {
                    if (item.Tag is string s && s == page)
                    {
                        NavView.SelectedItem = item;
                    }
                }
            }
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

    public void Receive(OpenExtensionGalleryScreenshotViewerMessage message)
    {
        if (message.Screenshots.Count == 0)
        {
            return;
        }

        OpenScreenshotViewer(message.Screenshot, message.Screenshots, startConnectedAnimation: true);
    }

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
        }
        else
        {
            AppTitleBar.IsPaneToggleButtonVisible = false;
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
        if (ScreenshotViewerPopup.IsOpen)
        {
            CloseScreenshotViewer();
            return;
        }

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
                if (KeyModifiers.GetCurrent().Alt)
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

    private void RootElement_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScreenshotViewerPopupSize();
    }

    private void HideBreadcrumb()
    {
        _breadcrumbStoryboard?.Stop();

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(fadeOut, BreadcrumbContainer);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");

        _breadcrumbStoryboard = new Storyboard();
        _breadcrumbStoryboard.Children.Add(fadeOut);
        _breadcrumbStoryboard.Completed += (_, _) =>
        {
            BreadcrumbContainer.Visibility = Visibility.Collapsed;
            BreadcrumbContainer.Opacity = 1;
            _breadcrumbStoryboard = null;
        };
        _breadcrumbStoryboard.Begin();
    }

    private void ShowBreadcrumb()
    {
        _breadcrumbStoryboard?.Stop();
        _breadcrumbStoryboard = null;

        if (BreadcrumbContainer.Visibility == Visibility.Collapsed)
        {
            BreadcrumbContainer.Opacity = 0;
            BreadcrumbContainer.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fadeIn, BreadcrumbContainer);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            _breadcrumbStoryboard = new Storyboard();
            _breadcrumbStoryboard.Children.Add(fadeIn);
            _breadcrumbStoryboard.Completed += (_, _) => _breadcrumbStoryboard = null;
            _breadcrumbStoryboard.Begin();
        }
        else
        {
            BreadcrumbContainer.Opacity = 1;
        }
    }

    public void Dispose()
    {
        CloseScreenshotViewer();
        WinGetOperationsButtonControl?.Dispose();
        _localKeyboardListener?.Dispose();
    }

    private void NavFrame_OnNavigated(object sender, NavigationEventArgs e)
    {
        BreadCrumbs.Clear();
        ShowBreadcrumb();

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
        else if (e.SourcePageType == typeof(ExtensionGalleryPage))
        {
            NavView.SelectedItem = GalleryPageNavItem;
            var pageType = RS_.GetString("Settings_PageTitles_GalleryPage");
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else if (e.SourcePageType == typeof(ExtensionGalleryItemPage) && e.Parameter is ExtensionGalleryItemViewModel galleryExtension)
        {
            NavView.SelectedItem = GalleryPageNavItem;
            HideBreadcrumb();
            var galleryPageType = RS_.GetString("Settings_PageTitles_GalleryPage");
            BreadCrumbs.Add(new(galleryPageType, "Gallery"));
            BreadCrumbs.Add(new(galleryExtension.Title, galleryExtension));
        }
        else if (e.SourcePageType == typeof(DockSettingsPage))
        {
            NavView.SelectedItem = DockSettingsPageNavItem;
            var pageType = RS_.GetString("Settings_PageTitles_DockPage");
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else if (e.SourcePageType == typeof(ExtensionPage) && e.Parameter is ProviderSettingsViewModel vm)
        {
            NavView.SelectedItem = ExtensionPageNavItem;
            var extensionsPageType = RS_.GetString("Settings_PageTitles_ExtensionsPage");
            BreadCrumbs.Add(new(extensionsPageType, extensionsPageType));
            BreadCrumbs.Add(new(vm.DisplayName, vm));
        }
        else if (e.SourcePageType == typeof(InternalPage) && _internalNavItem is not null)
        {
            NavView.SelectedItem = _internalNavItem;
            var pageType = "Internal";
            BreadCrumbs.Add(new(pageType, pageType));
        }
        else
        {
            BreadCrumbs.Add(new($"[{e.SourcePageType?.Name}]", string.Empty));
            Logger.LogError($"Unknown breadcrumb for page type '{e.SourcePageType}'");
        }
    }

    private void CloseScreenshotViewerButton_Click(object sender, RoutedEventArgs e)
    {
        CloseScreenshotViewer();
    }

    private void PreviousScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeScreenshot(-1);
    }

    private void NextScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeScreenshot(1);
    }

    private void ScreenshotViewerOverlay_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!ScreenshotViewerPopup.IsOpen || _currentScreenshotSet.Count <= 1)
        {
            return;
        }

        var delta = e.GetCurrentPoint(ScreenshotViewerOverlay).Properties.MouseWheelDelta;
        if (delta > 0)
        {
            ChangeScreenshot(-1);
            e.Handled = true;
        }
        else if (delta < 0)
        {
            ChangeScreenshot(1);
            e.Handled = true;
        }
    }

    private void ScreenshotViewerOverlay_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ScreenshotViewerPopup.IsOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                CloseScreenshotViewer();
                e.Handled = true;
                break;
            case VirtualKey.Left:
                ChangeScreenshot(-1);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                ChangeScreenshot(1);
                e.Handled = true;
                break;
        }
    }

    private void OpenScreenshotViewer(
        ExtensionGalleryScreenshotViewModel screenshot,
        IReadOnlyList<ExtensionGalleryScreenshotViewModel> screenshots,
        bool startConnectedAnimation)
    {
        _currentScreenshotSet = screenshots;
        _currentScreenshot = screenshot;
        _currentScreenshotViewerSource = CreateViewerImageSource(screenshot.Uri);
        UpdateScreenshotViewerBindings();
        UpdateScreenshotViewerPopupSize();
        ScreenshotViewerPopup.IsOpen = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            ScreenshotViewerOverlay.UpdateLayout();

            if (startConnectedAnimation)
            {
                var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(OpenExtensionGalleryScreenshotViewerMessage.ConnectedAnimationKey);
                animation?.TryStart(ScreenshotViewerImageHost);
            }

            ScreenshotViewerOverlay.Focus(FocusState.Programmatic);
        });
    }

    private void CloseScreenshotViewer()
    {
        if (ScreenshotViewerPopup.IsOpen)
        {
            ScreenshotViewerPopup.IsOpen = false;
        }

        _currentScreenshotSet = [];
        _currentScreenshot = null;
        _currentScreenshotViewerSource = null;
        UpdateScreenshotViewerBindings();
        RootElement.Focus(FocusState.Programmatic);
    }

    private void ChangeScreenshot(int delta)
    {
        if (_currentScreenshot is null || _currentScreenshotSet.Count <= 1)
        {
            return;
        }

        var currentIndex = GetCurrentScreenshotIndex();
        if (currentIndex < 0)
        {
            return;
        }

        var nextIndex = (currentIndex + delta) % _currentScreenshotSet.Count;
        if (nextIndex < 0)
        {
            nextIndex += _currentScreenshotSet.Count;
        }

        OpenScreenshotViewer(_currentScreenshotSet[nextIndex], _currentScreenshotSet, startConnectedAnimation: false);
    }

    private int GetCurrentScreenshotIndex()
    {
        if (_currentScreenshot is null)
        {
            return -1;
        }

        for (var i = 0; i < _currentScreenshotSet.Count; i++)
        {
            if (ReferenceEquals(_currentScreenshotSet[i], _currentScreenshot))
            {
                return i;
            }
        }

        return Math.Clamp(_currentScreenshot.Index, 0, _currentScreenshotSet.Count - 1);
    }

    private void UpdateScreenshotViewerPopupSize()
    {
        if (RootElement.ActualWidth <= 0 || RootElement.ActualHeight <= 0)
        {
            return;
        }

        ScreenshotViewerOverlay.Width = RootElement.ActualWidth;
        ScreenshotViewerOverlay.Height = RootElement.ActualHeight;
    }

    private void UpdateScreenshotViewerBindings()
    {
        Bindings.Update();
    }

    private static ImageSource? CreateViewerImageSource(Uri? screenshotUri)
    {
        if (screenshotUri is null)
        {
            return null;
        }

        BitmapImage bitmap = new();
        bitmap.UriSource = screenshotUri;
        return bitmap;
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
