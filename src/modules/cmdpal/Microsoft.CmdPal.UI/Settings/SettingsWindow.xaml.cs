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
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
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
    private readonly SettingsLinkContextMenuService _settingsLinkContextMenuService;
    private readonly ISettingsLinkResolver _settingsLinkResolver;
    private readonly ISettingsService _settingsService;
    private readonly SettingsTargetHighlighter _settingsTargetHighlighter = new();
    private readonly TopLevelCommandManager _topLevelCommandManager;

    private Storyboard? _breadcrumbStoryboard;
    private IReadOnlyList<ExtensionGalleryScreenshotViewModel> _currentScreenshotSet = [];
    private ExtensionGalleryScreenshotViewModel? _currentScreenshot;
    private CancellationTokenSource? _extensionSettingsNavigationCts;
    private CancellationTokenSource? _settingsTargetNavigationCts;

    public ObservableCollection<Crumb> BreadCrumbs { get; } = [];

    // Gets or sets optional action invoked after NavigationView is loaded.
    public Action? NavigationViewLoaded { get; set; }

    public string CurrentScreenshotDisplayName => _currentScreenshot?.DisplayName ?? string.Empty;

    public string CurrentScreenshotPositionText =>
        _currentScreenshot is null || _currentScreenshotSet.Count == 0
            ? string.Empty
            : $"{GetCurrentScreenshotIndex() + 1} / {_currentScreenshotSet.Count}";

    public SettingsWindow(
        TopLevelCommandManager topLevelCommandManager,
        ISettingsLinkResolver settingsLinkResolver,
        SettingsLinkContextMenuService settingsLinkContextMenuService,
        ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(topLevelCommandManager);
        ArgumentNullException.ThrowIfNull(settingsLinkResolver);
        ArgumentNullException.ThrowIfNull(settingsLinkContextMenuService);
        ArgumentNullException.ThrowIfNull(settingsService);

        _settingsLinkContextMenuService = settingsLinkContextMenuService;
        _settingsLinkResolver = settingsLinkResolver;
        _settingsService = settingsService;
        _topLevelCommandManager = topLevelCommandManager;

        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.SetIcon();
        var title = RS_.GetString("SettingsWindowTitle");
        this.AppWindow.Title = title;
        this.AppTitleBar.Title = title;
        TitleBarHelper.SetPreferredTheme(this);

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
                Tag = SettingsPageTags.Internal,
            };
            NavView.FooterMenuItems.Add(_internalNavItem);
        }
        else
        {
            _internalNavItem = null;
        }

        Navigate(SettingsPageTags.General);
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

    private void RootElement_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (_settingsLinkContextMenuService.TryShow(args, NavFrame.Content as Page))
        {
            args.Handled = true;
        }
    }

    private void RootElement_ContextCanceled(UIElement sender, RoutedEventArgs args)
    {
        _settingsLinkContextMenuService.Hide();
    }

    internal void Navigate(OpenSettingsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Navigate(
            message.SettingsPageTag,
            message.ExtensionGalleryId,
            message.SettingsLinkId,
            message.ExtensionProviderId);
    }

    internal void Navigate(
        string page,
        string? extensionGalleryId = null,
        string? settingsLinkId = null,
        string? extensionProviderId = null)
    {
        CancelSettingsNavigation();
        SettingsLinkFallbackInfoBar.IsOpen = false;

        string? settingsTarget = null;
        var settingsAction = SettingsLinkAction.None;
        var settingsLinkFallback = SettingsLinkFallback.None;
        if (settingsLinkId is not null)
        {
            if (page != string.Empty ||
                extensionGalleryId is not null)
            {
                Logger.LogError($"Invalid settings link navigation for '{settingsLinkId}'.");
                return;
            }

            var resolution = _settingsLinkResolver.Resolve(settingsLinkId, extensionProviderId);
            var destination = resolution.Destination;
            if (extensionProviderId is not null &&
                !destination.RequiresExtensionProvider)
            {
                Logger.LogError($"Invalid settings link navigation for '{settingsLinkId}'.");
                return;
            }

            settingsLinkFallback = resolution.Fallback;
            page = destination.PageTag;
            settingsTarget = destination.ElementId;
            settingsAction = destination.Action;
            if (!destination.RequiresExtensionProvider)
            {
                extensionProviderId = null;
            }
        }
        else if (extensionProviderId is not null)
        {
            Logger.LogError("Extension settings navigation requires a settings link ID.");
            return;
        }

        if (extensionProviderId is not null)
        {
            if (settingsAction != SettingsLinkAction.None)
            {
                Logger.LogError($"Settings action '{settingsAction}' cannot target an extension provider.");
                return;
            }

            NavigateToExtensionSettings(extensionProviderId, settingsTarget, settingsLinkId!, settingsLinkFallback);
            return;
        }

        Type? pageType;
        switch (page)
        {
            case SettingsPageTags.General:
                pageType = typeof(GeneralPage);
                break;
            case SettingsPageTags.Appearance:
                pageType = typeof(AppearancePage);
                break;
            case SettingsPageTags.Extensions:
                pageType = typeof(ExtensionsPage);
                break;
            case SettingsPageTags.Gallery:
                pageType = typeof(ExtensionGalleryPage);
                break;
            case SettingsPageTags.Dock:
                pageType = typeof(DockSettingsPage);
                break;
            case SettingsPageTags.Internal:
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

        if (pageType is null)
        {
            return;
        }

        var openGallery = pageType == typeof(ExtensionGalleryPage);
        var openGalleryExtension = openGallery && !string.IsNullOrWhiteSpace(extensionGalleryId);
        if (openGallery && TryOpenGallery(extensionGalleryId))
        {
            return;
        }

        if (pageType == typeof(ExtensionsPage) && TryReturnToExtensionsPage())
        {
            NavigateToSettingsTarget(settingsTarget, settingsAction, settingsLinkId);
            ShowSettingsLinkFallback(settingsLinkFallback);
            return;
        }

        if (NavFrame.Content?.GetType() == pageType)
        {
            NavigateToSettingsTarget(settingsTarget, settingsAction, settingsLinkId);
            ShowSettingsLinkFallback(settingsLinkFallback);
            return;
        }

        if (!NavFrame.Navigate(pageType))
        {
            Logger.LogWarning($"Could not open settings page '{pageType.Name}'.");
            return;
        }

        if (openGalleryExtension && NavFrame.Content is ExtensionGalleryPage galleryPage)
        {
            galleryPage.OpenExtension(extensionGalleryId!);
        }

        NavigateToSettingsTarget(settingsTarget, settingsAction, settingsLinkId);

        // Now, make sure to actually select the correct menu item too
        foreach (var obj in NavView.MenuItems)
        {
            if (obj is NavigationViewItem item && item.Tag is string s && s == page)
            {
                NavView.SelectedItem = item;
            }
        }

        ShowSettingsLinkFallback(settingsLinkFallback);
    }

    private void NavigateToSettingsTarget(
        string? settingsTarget,
        SettingsLinkAction settingsAction = SettingsLinkAction.None,
        string? settingsLinkId = null)
    {
        if (settingsTarget is null && settingsAction == SettingsLinkAction.None)
        {
            return;
        }

        if (NavFrame.Content is not FrameworkElement page)
        {
            Logger.LogWarning($"Settings target '{settingsTarget}' has no active page.");
            return;
        }

        var cancellation = new CancellationTokenSource();
        _settingsTargetNavigationCts = cancellation;
        _ = NavigateToSettingsTargetAsync(page, settingsTarget, settingsAction, settingsLinkId, cancellation);
    }

    private void ShowSettingsLinkFallback(SettingsLinkFallback fallback)
    {
        if (fallback == SettingsLinkFallback.None)
        {
            return;
        }

        var resource = fallback switch
        {
            SettingsLinkFallback.UnknownLink => "SettingsLink_Fallback_UnknownLink",
            SettingsLinkFallback.UnknownProvider => "SettingsLink_Fallback_UnknownProvider",
            SettingsLinkFallback.HiddenTarget => "SettingsLink_Fallback_HiddenTarget",
            SettingsLinkFallback.MissingTarget => "SettingsLink_Fallback_MissingTarget",
            SettingsLinkFallback.DisabledProvider => "SettingsLink_Fallback_DisabledProvider",
            _ => "SettingsLink_Fallback_Other",
        };
        SettingsLinkFallbackInfoBar.Title = RS_.GetString(
            fallback is SettingsLinkFallback.HiddenTarget or SettingsLinkFallback.MissingTarget or SettingsLinkFallback.DisabledProvider
                ? "SettingsLink_Fallback_UnavailableTitle"
                : "SettingsLink_Fallback_RedirectedTitle");
        SettingsLinkFallbackInfoBar.Message = RS_.GetString(resource);
        SettingsLinkFallbackInfoBar.IsOpen = true;
    }

    private void NavigateToExtensionSettings(string providerId, string? settingsTarget, string settingsLinkId, SettingsLinkFallback fallback)
    {
        var cancellation = new CancellationTokenSource();
        _extensionSettingsNavigationCts = cancellation;
        _ = NavigateToExtensionSettingsAsync(providerId, settingsTarget, settingsLinkId, fallback, cancellation);
    }

    private async Task NavigateToExtensionSettingsAsync(
        string providerId,
        string? settingsTarget,
        string settingsLinkId,
        SettingsLinkFallback fallback,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (_topLevelCommandManager.IsLoading)
            {
                await _topLevelCommandManager.WaitForCurrentLoadAsync(cancellation.Token);
            }

            cancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_extensionSettingsNavigationCts, cancellation))
            {
                return;
            }

            _extensionSettingsNavigationCts = null;
            OpenExtensionSettings(providerId, settingsTarget, settingsLinkId, fallback);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to navigate to extension settings for '{providerId}'.", ex);
        }
        finally
        {
            if (ReferenceEquals(_extensionSettingsNavigationCts, cancellation))
            {
                _extensionSettingsNavigationCts = null;
            }

            cancellation.Dispose();
        }
    }

    private void OpenExtensionSettings(string providerId, string? settingsTarget, string settingsLinkId, SettingsLinkFallback fallback)
    {
        if (NavFrame.Content is ExtensionPage extensionPage)
        {
            if (string.Equals(extensionPage.ViewModel?.ProviderId, providerId, StringComparison.Ordinal))
            {
                NavigateToSettingsTarget(settingsTarget, settingsLinkId: settingsLinkId);
                ShowSettingsLinkFallback(fallback);
                return;
            }

            _ = TryReturnToExtensionsPage();
        }

        if (NavFrame.Content is not ExtensionsPage)
        {
            NavFrame.Navigate(typeof(ExtensionsPage));
        }

        if (NavFrame.Content is not ExtensionsPage extensionsPage)
        {
            Logger.LogWarning($"Could not open the Extensions page for provider '{providerId}'.");
            return;
        }

        var extension = extensionsPage.FindProvider(providerId);
        if (extension is null)
        {
            Logger.LogWarning($"Unknown extension settings provider '{providerId}'.");
            ShowSettingsLinkFallback(SettingsLinkFallback.UnknownProvider);
            return;
        }

        if (!NavFrame.Navigate(typeof(ExtensionPage), extension))
        {
            Logger.LogWarning($"Could not open the settings page for provider '{providerId}'.");
            return;
        }

        NavigateToSettingsTarget(settingsTarget, settingsLinkId: settingsLinkId);
        ShowSettingsLinkFallback(fallback);
    }

    private bool TryReturnToExtensionsPage()
    {
        if (NavFrame.Content is ExtensionsPage)
        {
            return true;
        }

        if (NavFrame.Content is not ExtensionPage ||
            !NavFrame.CanGoBack ||
            NavFrame.BackStack.Count == 0 ||
            NavFrame.BackStack[NavFrame.BackStack.Count - 1].SourcePageType != typeof(ExtensionsPage))
        {
            return false;
        }

        NavFrame.GoBack();
        NavFrame.ForwardStack.Clear();
        return NavFrame.Content is ExtensionsPage;
    }

    private async Task NavigateToSettingsTargetAsync(
        FrameworkElement page,
        string? settingsTarget,
        SettingsLinkAction settingsAction,
        string? settingsLinkId,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (settingsTarget is not null)
            {
                var (target, isHidden) = await SettingsPageTarget.NavigateAsync(page, settingsTarget, cancellation.Token);
                if (target is null)
                {
                    Logger.LogWarning($"Unavailable settings target '{settingsTarget}' for page '{page.GetType().Name}'.");
                    if (settingsLinkId is not null)
                    {
                        var fallback = _settingsLinkResolver.ClassifyUnavailableTarget(
                            isHidden,
                            page is ExtensionPage { ViewModel.IsEnabled: false });
                        ShowSettingsLinkFallback(fallback);
                    }

                    return;
                }

                _settingsTargetHighlighter.Highlight(target, !_settingsService.Settings.DisableAnimations);
            }
            else
            {
                await SettingsPageTarget.WaitForLoadedAsync(page, cancellation.Token);
            }

            await InvokeSettingsLinkActionAsync(page, settingsAction, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to navigate to settings target '{settingsTarget}'.", ex);
        }
        finally
        {
            if (ReferenceEquals(_settingsTargetNavigationCts, cancellation))
            {
                _settingsTargetNavigationCts = null;
            }

            cancellation.Dispose();
        }
    }

    private static async Task InvokeSettingsLinkActionAsync(
        FrameworkElement page,
        SettingsLinkAction settingsAction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (settingsAction)
        {
            case SettingsLinkAction.None:
                return;
            case SettingsLinkAction.OpenFallbackOrder when page is ExtensionsPage extensionsPage:
                await extensionsPage.ShowFallbackOrderDialogAsync();
                return;
            default:
                throw new InvalidOperationException(
                    $"Settings action '{settingsAction}' is invalid for page '{page.GetType().Name}'.");
        }
    }

    private void CancelSettingsNavigation()
    {
        _extensionSettingsNavigationCts?.Cancel();
        _extensionSettingsNavigationCts = null;
        _settingsTargetNavigationCts?.Cancel();
        _settingsTargetNavigationCts = null;
        _settingsTargetHighlighter.Clear();
    }

    private bool TryOpenGallery(string? extensionId)
    {
        var openExtension = !string.IsNullOrWhiteSpace(extensionId);
        if (NavFrame.Content is ExtensionGalleryItemPage itemPage)
        {
            if (openExtension && string.Equals(itemPage.ViewModel?.Id, extensionId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Details is a child of the gallery on the same frame. Return to the existing
            // gallery entry so the next details page replaces this one in the journal.
            if (NavFrame.CanGoBack &&
                NavFrame.BackStack.Count > 0 &&
                NavFrame.BackStack[NavFrame.BackStack.Count - 1].SourcePageType == typeof(ExtensionGalleryPage))
            {
                NavFrame.GoBack();
                NavFrame.ForwardStack.Clear();
            }
        }

        if (NavFrame.Content is not ExtensionGalleryPage galleryPage)
        {
            return false;
        }

        galleryPage.ClearPendingExtension();

        if (openExtension)
        {
            galleryPage.OpenExtension(extensionId!);
        }

        return true;
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
        CancelSettingsNavigation();
        _settingsLinkContextMenuService.Hide();
        CloseScreenshotViewer();
        _settingsTargetHighlighter.Close();
        WinGetOperationsButtonControl?.Dispose();
        _localKeyboardListener?.Dispose();
    }

    private void NavFrame_OnNavigated(object sender, NavigationEventArgs e)
    {
        CancelSettingsNavigation();
        SettingsLinkFallbackInfoBar.IsOpen = false;

        BreadCrumbs.Clear();
        ShowBreadcrumb();

        if (e.SourcePageType == typeof(GeneralPage))
        {
            NavView.SelectedItem = GeneralPageNavItem;
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_GeneralPage"), SettingsPageTags.General));
        }
        else if (e.SourcePageType == typeof(AppearancePage))
        {
            NavView.SelectedItem = AppearancePageNavItem;
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_AppearancePage"), SettingsPageTags.Appearance));
        }
        else if (e.SourcePageType == typeof(ExtensionsPage))
        {
            NavView.SelectedItem = ExtensionPageNavItem;
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_ExtensionsPage"), SettingsPageTags.Extensions));
        }
        else if (e.SourcePageType == typeof(ExtensionGalleryPage))
        {
            NavView.SelectedItem = GalleryPageNavItem;
            HideBreadcrumb();
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_GalleryPage"), SettingsPageTags.Gallery));
        }
        else if (e.SourcePageType == typeof(ExtensionGalleryItemPage) && e.Parameter is ExtensionGalleryItemViewModel galleryExtension)
        {
            NavView.SelectedItem = GalleryPageNavItem;
            HideBreadcrumb();
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_GalleryPage"), SettingsPageTags.Gallery));
            BreadCrumbs.Add(new(galleryExtension.Title, galleryExtension));
        }
        else if (e.SourcePageType == typeof(DockSettingsPage))
        {
            NavView.SelectedItem = DockSettingsPageNavItem;
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_DockPage"), SettingsPageTags.Dock));
        }
        else if (e.SourcePageType == typeof(ExtensionPage) && e.Parameter is ProviderSettingsViewModel vm)
        {
            NavView.SelectedItem = ExtensionPageNavItem;
            BreadCrumbs.Add(new(RS_.GetString("Settings_PageTitles_ExtensionsPage"), SettingsPageTags.Extensions));
            BreadCrumbs.Add(new(vm.DisplayName, vm));
        }
        else if (e.SourcePageType == typeof(InternalPage) && _internalNavItem is not null)
        {
            NavView.SelectedItem = _internalNavItem;
            BreadCrumbs.Add(new(SettingsPageTags.Internal, SettingsPageTags.Internal));
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

    private void ScreenshotViewerFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentScreenshot = ScreenshotViewerFlipView.SelectedItem as ExtensionGalleryScreenshotViewModel;
        UpdateScreenshotViewerBindings();
    }

    private void OpenScreenshotViewer(
        ExtensionGalleryScreenshotViewModel screenshot,
        IReadOnlyList<ExtensionGalleryScreenshotViewModel> screenshots,
        bool startConnectedAnimation)
    {
        _currentScreenshotSet = screenshots;
        UpdateScreenshotViewerBindings();
        UpdateScreenshotViewerPopupSize();
        ScreenshotViewerFlipView.ItemsSource = screenshots;

        // _currentScreenshot has to be set after ItemsSource for the FlipView to update to the correct index
        _currentScreenshot = screenshot;
        ScreenshotViewerFlipView.SelectedIndex = GetCurrentScreenshotIndex();
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

        ScreenshotViewerFlipView.ItemsSource = null;
        ScreenshotViewerFlipView.SelectedIndex = -1;
        _currentScreenshotSet = [];
        _currentScreenshot = null;
        UpdateScreenshotViewerBindings();
        RootElement.Focus(FocusState.Programmatic);
    }

    private void ChangeScreenshot(int delta)
    {
        if (_currentScreenshotSet.Count <= 1 || ScreenshotViewerFlipView.SelectedIndex < 0)
        {
            return;
        }

        var nextIndex = (ScreenshotViewerFlipView.SelectedIndex + delta) % _currentScreenshotSet.Count;
        if (nextIndex < 0)
        {
            nextIndex += _currentScreenshotSet.Count;
        }

        ScreenshotViewerFlipView.SelectedIndex = nextIndex;
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
