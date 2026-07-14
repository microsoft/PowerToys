// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.Settings;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using VirtualKey = Windows.System.VirtualKey;

namespace Microsoft.CmdPal.UI.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ShellPage : Microsoft.UI.Xaml.Controls.Page,
    IRecipient<NavigateBackMessage>,
    IRecipient<OpenSettingsMessage>,
    IRecipient<HotkeySummonMessage>,
    IRecipient<FocusSearchBoxMessage>,
    IRecipient<ShowDetailsMessage>,
    IRecipient<HideDetailsMessage>,
    IRecipient<ClearSearchMessage>,
    IRecipient<LaunchUriMessage>,
    IRecipient<SettingsWindowClosedMessage>,
    IRecipient<GoHomeMessage>,
    IRecipient<GoBackMessage>,
    IRecipient<ShowConfirmationMessage>,
    IRecipient<ShowToastMessage>,
    IRecipient<NavigateToPageMessage>,
    IRecipient<ShowHideDockMessage>,
    IRecipient<ShowPinToDockDialogMessage>,
    IRecipient<ExpandCompactModeMessage>,
    INotifyPropertyChanged,
    IDisposable
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };
    private readonly SuppressNavigationTransitionInfo _noAnimation = new();

    private readonly ToastWindow _toast = new();

    private readonly CompositeFormat _pageNavigatedAnnouncement;

    private readonly ISettingsService _settingsService;

    // The last compact-mode setting we reacted to. Lets us ignore hot-reloads of unrelated
    // settings and only re-evaluate the layout when compact mode itself changes.
    private bool _compactMode;

    private SettingsWindow? _settingsWindow;
    private DockWindowManager? _dockWindowManager;

    private CancellationTokenSource? _focusAfterLoadedCts;
    private WeakReference<Page>? _lastNavigatedPageRef;

    // When the shell goes from compact (collapsed) to expanded, the content frame's page
    // — which was collapsed and therefore never laid out — finally fires its Loaded event.
    // That late Loaded would otherwise run the post-navigation focus/select logic and
    // select-all the character the user just typed (which triggered the expand). This
    // one-shot flag suppresses that select for the expand-driven load.
    private bool _suppressSelectOnNextLoad;
    private bool _pendingTopBarFocusRestore;
    private bool _isDisposed;

    public ShellViewModel ViewModel { get; private set; } = App.Current.Services.GetService<ShellViewModel>()!;

    public event PropertyChangedEventHandler? PropertyChanged;

    private IHostWindow? _hostWindow;

    public IHostWindow? HostWindow
    {
        get => _hostWindow;
        set
        {
            if (ReferenceEquals(_hostWindow, value))
            {
                return;
            }

            if (_hostWindow is not null)
            {
                _hostWindow.IsVisibleToUserChanged -= HostWindow_IsVisibleToUserChanged;
            }

            _hostWindow = value;

            if (_hostWindow is not null)
            {
                _hostWindow.IsVisibleToUserChanged += HostWindow_IsVisibleToUserChanged;
            }
        }
    }

    public bool ExpandedMode { get; set; }

    // Item keybindings act on the selected item, which is hidden while collapsed — only honor them when expanded.
    private bool ItemActionsAllowed => !_compactMode || ExpandedMode;

    public ShellPage()
    {
        _settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        _compactMode = _settingsService.Settings.CompactMode;
        this.ExpandedMode = !_compactMode;

        this.InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.Register<NavigateBackMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<HotkeySummonMessage>(this);
        WeakReferenceMessenger.Default.Register<FocusSearchBoxMessage>(this);
        WeakReferenceMessenger.Default.Register<SettingsWindowClosedMessage>(this);

        WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(this);
        WeakReferenceMessenger.Default.Register<HideDetailsMessage>(this);

        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(this);
        WeakReferenceMessenger.Default.Register<LaunchUriMessage>(this);

        WeakReferenceMessenger.Default.Register<GoHomeMessage>(this);
        WeakReferenceMessenger.Default.Register<GoBackMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowConfirmationMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToPageMessage>(this);

        WeakReferenceMessenger.Default.Register<ShowHideDockMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowPinToDockDialogMessage>(this);

        WeakReferenceMessenger.Default.Register<ExpandCompactModeMessage>(this);

        // The compact-mode setting can be toggled while the palette is open. React to the
        // hot-reload so the expanded/collapsed layout updates immediately instead of waiting
        // for the next navigation or search-text change.
        _settingsService.SettingsChanged += OnSettingsChanged;

        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(ShellPage_OnPreviewKeyDown), true);
        AddHandler(KeyDownEvent, new KeyEventHandler(ShellPage_OnKeyDown), false);
        AddHandler(PointerPressedEvent, new PointerEventHandler(ShellPage_OnPointerPressed), true);

        RootFrame.Navigate(typeof(LoadingPage), new AsyncNavigationRequest(ViewModel, CancellationToken.None));

        var pageAnnouncementFormat = ResourceLoaderInstance.GetString("ScreenReader_Announcement_NavigatedToPage0");
        _pageNavigatedAnnouncement = CompositeFormat.Parse(pageAnnouncementFormat);

        if (App.Current.Services.GetRequiredService<ISettingsService>().Settings.EnableDock)
        {
            _dockWindowManager = App.Current.Services.GetService<DockWindowManager>();
            _dockWindowManager?.ShowDocks();
        }
    }

    /// <summary>
    /// Gets the default page animation, depending on the settings
    /// </summary>
    private NavigationTransitionInfo DefaultPageAnimation
    {
        get
        {
            var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
            return settings.DisableAnimations ? _noAnimation : _slideRightTransition;
        }
    }

    public void Receive(NavigateBackMessage message)
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;

        if (RootFrame.CanGoBack)
        {
            if (!message.FromBackspace ||
                settings.BackspaceGoesBack)
            {
                GoBack();
            }
        }
        else
        {
            if (!message.FromBackspace)
            {
                // If we can't go back then we must be at the top and thus escape again should quit.
                WeakReferenceMessenger.Default.Send(new DismissMessage());

                PowerToysTelemetry.Log.WriteEvent(new CmdPalDismissedOnEsc());
            }
        }
    }

    public void Receive(NavigateToPageMessage message)
    {
        // TODO GH #526 This needs more better locking too
        _ = _queue.TryEnqueue(DispatcherQueuePriority.High, () =>
        {
            // Also hide our details pane about here, if we had one
            HideDetails();

            // Navigate to the appropriate host page for that VM
            RootFrame.Navigate(
                message.Page switch
                {
                    ListViewModel => typeof(ListPage),
                    ContentPageViewModel => typeof(ContentPage),
                    ParametersPageViewModel => typeof(ParametersPage),
                    _ => throw new NotSupportedException(),
                },
                new AsyncNavigationRequest(message.Page, message.CancellationToken),
                message.WithAnimation ? DefaultPageAnimation : _noAnimation);

            PowerToysTelemetry.Log.WriteEvent(new OpenPage(RootFrame.BackStackDepth, message.Page.Id));

            // Telemetry: Send navigation depth for session max depth tracking
            WeakReferenceMessenger.Default.Send(new NavigationDepthMessage(RootFrame.BackStackDepth));

            if (!ViewModel.IsNested)
            {
                // todo BODGY
                RootFrame.BackStack.Clear();
            }
        });
    }

    public void Receive(ShowConfirmationMessage message)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await HandleConfirmArgsOnUiThread(message.Args);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        });
    }

    public void Receive(ShowToastMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _toast.ShowToast(message);
        });
    }

    public void Receive(ShowPinToDockDialogMessage message)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await HandlePinToDockDialogOnUiThread(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        });
    }

    private async Task HandlePinToDockDialogOnUiThread(ShowPinToDockDialogMessage message)
    {
        // Ask each dock window to display a teaching tip identifying its monitor,
        // so the user can correlate the dialog's monitor list with the physical docks.
        WeakReferenceMessenger.Default.Send(new ShowDockMonitorLabelsMessage(true));

        try
        {
            var (result, content) = await PinToDockDialogContent.ShowAsync(
                this.XamlRoot,
                message.Title,
                message.Subtitle,
                message.Icon,
                message.DockSide,
                message.AvailableMonitors);

            if (result == ContentDialogResult.Primary)
            {
                var pinMessage = new PinToDockMessage(
                    message.ProviderId,
                    message.CommandId,
                    Pin: true,
                    Side: content.SelectedSide,
                    ShowTitles: content.ShowTitles,
                    ShowSubtitles: content.ShowSubtitles,
                    MonitorDeviceId: content.SelectedMonitorDeviceId);
                WeakReferenceMessenger.Default.Send(pinMessage);
            }
        }
        finally
        {
            // Hide the teaching tips once the dialog is saved or dismissed.
            WeakReferenceMessenger.Default.Send(new ShowDockMonitorLabelsMessage(false));
        }
    }

    // This gets called from the UI thread
    private async Task HandleConfirmArgsOnUiThread(IConfirmationArgs? args)
    {
        if (args is null)
        {
            return;
        }

        ConfirmResultViewModel vm = new(args, new(ViewModel.CurrentPage));
        var initializeDialogTask = Task.Run(() => { InitializeConfirmationDialog(vm); });
        await initializeDialogTask;

        var resourceLoader = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance.ResourceLoader;
        var confirmText = resourceLoader.GetString("ConfirmationDialog_ConfirmButtonText");
        var cancelText = resourceLoader.GetString("ConfirmationDialog_CancelButtonText");

        var name = string.IsNullOrEmpty(vm.PrimaryCommand.Name) ? confirmText : vm.PrimaryCommand.Name;
        ContentDialog dialog = new()
        {
            Title = vm.Title,
            Content = vm.Description,
            PrimaryButtonText = name,
            CloseButtonText = cancelText,
            XamlRoot = this.XamlRoot,
        };

        if (vm.IsPrimaryCommandCritical)
        {
            dialog.DefaultButton = ContentDialogButton.Close;

            // TODO: Maybe we need to style the primary button to be red?
            // dialog.PrimaryButtonStyle = new Style(typeof(Button))
            // {
            //     Setters =
            //     {
            //         new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.Red)),
            //         new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.Red)),
            //     },
            // };
        }

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var performMessage = new PerformCommandMessage(vm);
            WeakReferenceMessenger.Default.Send(performMessage);
        }
        else
        {
            // cancel
        }
    }

    private void InitializeConfirmationDialog(ConfirmResultViewModel vm) => vm.SafeInitializePropertiesSynchronous();

    public void Receive(OpenSettingsMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            OpenSettings(message.SettingsPageTag);
        });
    }

    public void OpenSettings(string pageTag)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
        }

        _settingsWindow.Activate();
        _settingsWindow.BringToFront();
        _settingsWindow.Navigate(pageTag);
    }

    public void Receive(ShowDetailsMessage message)
    {
        if (ViewModel is null || ViewModel.CurrentPage is null)
        {
            return;
        }

        var details = message.Details;
        var wasVisible = ViewModel.IsDetailsVisible;

        // GH #322:
        // For inexplicable reasons, if you try to change the details too fast,
        // we'll explode. This seemingly only happens if you change the details
        // while we're also scrolling a new list view item into view.
        //
        // Always debounce through the DispatcherQueue
        // timer so the UI settles between updates. Use immediate=true for
        // the first show so the panel appears without delay; subsequent
        // updates during rapid navigation are coalesced.
        _debounceTimer.Debounce(ShowDetails, interval: TimeSpan.FromMilliseconds(100), immediate: !wasVisible);

        void ShowDetails()
        {
            // Since immediate=true means we're called synchronously from this method, we need to check
            // if we're on the UI thread and re-queue if not.
            if (!_queue.HasThreadAccess)
            {
                var enqueued = _queue.TryEnqueue(ShowDetails);
                if (!enqueued)
                {
                    Logger.LogError("Failed to enqueue show details action on UI thread");
                }

                return;
            }

            try
            {
                DetailsContent.ChangeView(null, 0, null, true);
                ViewModel.Details = details;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHeroImage)));
                ViewModel.IsDetailsVisible = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to show detail", ex);
            }
        }
    }

    public void Receive(HideDetailsMessage message)
    {
        // Debounce the hide through the same timer used for show. If a
        // ShowDetailsMessage arrives before this fires, it cancels the
        // pending hide - preventing the panel from flickering closed and
        // reopened during rapid item navigation.
        _debounceTimer.Debounce(
            () => HideDetails(),
            interval: TimeSpan.FromMilliseconds(150),
            immediate: false);
    }

    public void Receive(LaunchUriMessage message) => _ = global::Windows.System.Launcher.LaunchUriAsync(message.Uri);

    private void HideDetails()
    {
        ViewModel.Details = null;
        ViewModel.IsDetailsVisible = false;
    }

    public void Receive(ClearSearchMessage message) => SearchBox.ClearSearch();

    public void Receive(FocusSearchBoxMessage message) => RequestTopBarFocusRestore();

    public void Receive(HotkeySummonMessage message) => _ = DispatcherQueue.TryEnqueue(() => SummonOnUiThread(message));

    public void Receive(SettingsWindowClosedMessage message) => _settingsWindow = null;

    private void SummonOnUiThread(HotkeySummonMessage message)
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        var commandId = message.CommandId;
        var isRoot = string.IsNullOrEmpty(commandId);
        if (isRoot)
        {
            // If this is the hotkey for the root level, then always show us
            WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));

            // Depending on the settings, either
            // * Go home, or
            // * Select the search text (if we should remain open on this page)
            if (settings.AutoGoHomeInterval == TimeSpan.Zero)
            {
                GoHome(false);
            }
            else if (settings.HighlightSearchOnActivate)
            {
                SearchBox.SelectSearch();
            }
        }
        else
        {
            try
            {
                // For a hotkey bound to a command, first lookup the
                // command from our list of toplevel commands.
                var tlcManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
                var topLevelCommand = tlcManager.LookupCommand(commandId);
                if (topLevelCommand is not null)
                {
                    var command = topLevelCommand.CommandViewModel.Model.Unsafe;
                    var isPage = command is not IInvokableCommand;

                    // If the bound command is an invokable command, then
                    // we don't want to open the window at all - we want to
                    // just do it.
                    if (isPage)
                    {
                        // If we're here, then the bound command was a page
                        // of some kind. Reset to root (clearing any transient dock state),
                        // show the window, and navigate to it.
                        ViewModel.ResetToHome();

                        WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));
                    }

                    var msg = topLevelCommand.GetPerformCommandMessage();
                    msg.WithAnimation = false;
                    WeakReferenceMessenger.Default.Send<PerformCommandMessage>(msg);

                    // we can't necessarily SelectSearch() here, because when the page is loaded,
                    // we'll fetch the SearchText from the page itself, and that'll stomp the
                    // selection we start now.
                    // That's probably okay though.
                }
            }
            catch
            {
            }
        }

        // When re-showing the palette, the previous session's query may still be present
        // (e.g. after a light dismiss with HighlightSearchOnActivate). Recompute the
        // compact/expanded state so a retained query restores the expanded results instead
        // of being stuck in the collapsed search-only layout.
        UpdateCompactModeForCurrentPage();

        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    public void Receive(GoBackMessage message) => _ = DispatcherQueue.TryEnqueue(() => GoBack(message.WithAnimation, message.FocusSearch));

    private void GoBack(bool withAnimation = true, bool focusSearch = true)
    {
        HideDetails();

        ViewModel.CancelNavigation();

        // Note: That we restore the VM state below in RootFrame_Navigated call back after this occurs.
        // In the future, we may want to manage the back stack ourselves vs. relying on Frame
        // We could replace Frame with a ContentPresenter, but then have to manage transition animations ourselves.
        // However, then we have more fine-grained control on the back stack, managing the VM cache, and not
        // having that all be a black box, though then we wouldn't cache the XAML page itself, but sometimes that is a drawback.
        // However, we do a good job here, see ForwardStack.Clear below, and BackStack.Clear above about managing that.
        if (RootFrame.CanGoBack)
        {
            if (withAnimation)
            {
                RootFrame.GoBack();
            }
            else
            {
                RootFrame.GoBack(_noAnimation);
            }

            // Don't store pages we're navigating away from in the Frame cache
            // TODO: In the future we probably want a short cache (3-5?) of recent VMs in case the user re-navigates
            // back to a recent page they visited (like the Pokedex) so we don't have to reload it from  scratch.
            // That'd be retrieved as we re-navigate in the PerformCommandMessage logic above
            RootFrame.ForwardStack.Clear();
        }

        if (!RootFrame.CanGoBack)
        {
            ViewModel.GoHome(withAnimation, focusSearch);
        }

        // Only move focus when the palette is actually on screen. FocusActiveControl uses keyboard
        // focus so the screen reader announces the box; doing that while the window is hidden
        // would announce it prematurely.
        if (focusSearch && HostWindow?.IsVisibleToUser == true)
        {
            SearchBox.FocusActiveControl();
            SearchBox.SelectSearch();
        }
    }

    public void Receive(GoHomeMessage message) => _ = DispatcherQueue.TryEnqueue(() => GoHome(withAnimation: message.WithAnimation, focusSearch: message.FocusSearch));

    private void GoHome(bool withAnimation = true, bool focusSearch = true)
    {
        while (RootFrame.CanGoBack)
        {
            // don't focus on each step, just at the end
            GoBack(withAnimation, focusSearch: false);
        }

        // focus search box, even if we were already home (but only when the palette is on
        // screen - see GoBack; keyboard focus while hidden announces prematurely).
        if (focusSearch && HostWindow?.IsVisibleToUser == true)
        {
            SearchBox.FocusActiveControl();
            SearchBox.SelectSearch();
        }
    }

    public void Receive(ShowHideDockMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            if (message.ShowDock)
            {
                if (_dockWindowManager is null)
                {
                    _dockWindowManager = App.Current.Services.GetService<DockWindowManager>();
                }

                _dockWindowManager?.ShowDocks();
            }
            else
            {
                _dockWindowManager?.HideDocks();
            }
        });
    }

    private void ToggleFilterFocus()
    {
        if (!FiltersDropDown.IsFilterVisible)
        {
            return;
        }

        if (FiltersDropDown.IsActive)
        {
            FiltersDropDown.CloseDropDownAndFocusSearch();
        }
        else
        {
            FiltersDropDown.OpenDropDown();
        }
    }

    private void SearchBox_ActiveFocusTargetChanged(object? sender, EventArgs e)
    {
        RequestTopBarFocusRestore();
    }

    private void HostWindow_IsVisibleToUserChanged(object? sender, EventArgs e)
    {
        if (HostWindow?.IsVisibleToUser == true &&
            _pendingTopBarFocusRestore &&
            ViewModel.CurrentPage?.HasSearchBox == true)
        {
            _pendingTopBarFocusRestore = false;
            SearchBox.FocusActiveControl();
        }
    }

    private void RequestTopBarFocusRestore()
    {
        if (ViewModel.CurrentPage?.HasSearchBox != true)
        {
            _pendingTopBarFocusRestore = false;
            return;
        }

        if (HostWindow?.IsVisibleToUser == true)
        {
            _pendingTopBarFocusRestore = false;
            SearchBox.FocusActiveControl();
            return;
        }

        _pendingTopBarFocusRestore = true;
    }

    private void BackButton_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());

    private void RootFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // A real navigation always loads a fresh page that we do want to focus/select, so
        // clear any stale suppression left over from a prior compact expand. (If this
        // navigation itself expands compact mode, UpdateCompactModeForCurrentPage below
        // will re-arm the flag for the page that's about to load.)
        _suppressSelectOnNextLoad = false;

        // This listens to the root frame to ensure that we also track the content's page VM as well that we passed as a parameter.
        // This is currently used for both forward and backward navigation.
        // As when we go back that we restore ourselves to the proper state within our VM
        if (e.Parameter is AsyncNavigationRequest request)
        {
            if (request.NavigationToken.IsCancellationRequested && e.NavigationMode is not (Microsoft.UI.Xaml.Navigation.NavigationMode.Back or Microsoft.UI.Xaml.Navigation.NavigationMode.Forward))
            {
                return;
            }

            switch (request.TargetViewModel)
            {
                case PageViewModel pageViewModel:
                    ViewModel.CurrentPage = pageViewModel;
                    break;
                case ShellViewModel:
                    // This one is an exception, for now (LoadingPage is tied to ShellViewModel,
                    // but ShellViewModel is not PageViewModel.
                    ViewModel.CurrentPage = ViewModel.NullPage;
                    break;
                default:
                    ViewModel.CurrentPage = ViewModel.NullPage;
                    Logger.LogWarning($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(PageViewModel)}");
                    break;
            }
        }
        else
        {
            Logger.LogWarning("Unrecognized target for shell navigation: " + e.Parameter);
        }

        if (e.Content is Page element)
        {
            _lastNavigatedPageRef = new WeakReference<Page>(element);
            element.Loaded += FocusAfterLoaded;
        }

        UpdateCompactModeForCurrentPage();
    }

    /// <summary>
    /// Updates the compact/expanded state after a navigation. On any nested (sub) page we
    /// always show the full expanded UI; on the root page the search box drives the state,
    /// so we collapse to the compact search box only when the query is empty. Driving this
    /// from navigation (rather than only from search-text changes) makes alias-based
    /// navigation expand correctly — an alias clears the search box before navigating, so
    /// the search-text transition alone would otherwise leave the palette collapsed.
    /// Transient pages always show the expanded UI, ignoring the compact setting entirely.
    /// </summary>
    private void UpdateCompactModeForCurrentPage()
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        if (!settings.CompactMode)
        {
            // Compact mode is off: the shell always shows the full expanded UI. Set it
            // explicitly (rather than trusting the constructor's initial value) so toggling
            // the setting off at runtime restores the list and command bar when the palette
            // was collapsed.
            HandleExpandCompactOnUiThread(true);
            return;
        }

        // Transient pages ignore compact mode and always present as expanded.
        if (ViewModel.IsTransient)
        {
            HandleExpandCompactOnUiThread(true);
            return;
        }

        // The ShellViewModel's IsNested flag is only updated on forward navigation and is
        // never cleared when navigating back to the root page. Gate it on the current
        // page's own root-ness so a stale IsNested can't keep the home page expanded after
        // returning to it (e.g. after following a 1-character alias and going back).
        var isRootPage = ViewModel.CurrentPage?.IsRootPage ?? false;
        var nested = ViewModel.IsNested && !isRootPage;
        var hasQuery = !string.IsNullOrEmpty(ViewModel.CurrentPage?.SearchTextBox);
        HandleExpandCompactOnUiThread(nested || hasQuery);
    }

    private void FocusAfterLoaded(object sender, RoutedEventArgs e)
    {
        var page = (Page)sender;
        page.Loaded -= FocusAfterLoaded;

        // Only handle focus for the latest navigated page
        if (_lastNavigatedPageRef is null || !_lastNavigatedPageRef.TryGetTarget(out var last) || !ReferenceEquals(page, last))
        {
            return;
        }

        // Cancel any previous pending focus work
        _focusAfterLoadedCts?.Cancel();
        _focusAfterLoadedCts?.Dispose();
        _focusAfterLoadedCts = new CancellationTokenSource();
        var token = _focusAfterLoadedCts.Token;

        AnnounceNavigationToPage(page);

        var shouldSearchBoxBeVisible = ViewModel.CurrentPage?.HasSearchBox ?? false;

        if (shouldSearchBoxBeVisible || page is not ContentPage)
        {
            ViewModel.IsSearchBoxVisible = shouldSearchBoxBeVisible;

            if (HostWindow?.IsVisibleToUser != true)
            {
                return;
            }

            // This Loaded can fire late when expanding out of compact mode (the page was
            // collapsed and never laid out). In that case the user is mid-typing in the
            // already-focused search box, so don't steal focus / select-all their input.
            if (_suppressSelectOnNextLoad)
            {
                _suppressSelectOnNextLoad = false;
                return;
            }

            SearchBox.FocusActiveControl();
            SearchBox.SelectSearch();
        }
        else
        {
            _ = Task.Run(
                async () =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        if (HostWindow?.IsVisibleToUser != true)
                        {
                            return;
                        }

                        await page.DispatcherQueue.EnqueueAsync(
                            async () =>
                            {
                                // I hate this so much, but it can take a while for the page to be ready to accept focus;
                                // focusing page with MarkdownTextBlock takes up to 5 attempts (* 100ms delay between attempts)
                                for (var i = 0; i < 10; i++)
                                {
                                    token.ThrowIfCancellationRequested();

                                    if (HostWindow?.IsVisibleToUser != true)
                                    {
                                        break;
                                    }

                                    if (FocusManager.FindFirstFocusableElement(page) is FrameworkElement frameworkElement)
                                    {
                                        var set = frameworkElement.Focus(FocusState.Programmatic);
                                        if (set)
                                        {
                                            break;
                                        }
                                    }

                                    await Task.Delay(100, token);
                                }

                                token.ThrowIfCancellationRequested();

                                // Update the search box visibility based on the current page:
                                // - We do this here after navigation so the focus is not jumping around too much,
                                //   it messes with screen readers if we do it too early
                                // - Since this should hide the search box on content pages, it's not a problem if we
                                //   wait for the code above to finish trying to focus the content
                                ViewModel.IsSearchBoxVisible = ViewModel.CurrentPage?.HasSearchBox ?? false;
                            });
                    }
                    catch (OperationCanceledException)
                    {
                        // Swallow cancellation - another FocusAfterLoaded invocation superseded this one
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error during FocusAfterLoaded async focus work", ex);
                    }
                },
                token);
        }
    }

    private void AnnounceNavigationToPage(Page page)
    {
        var pageTitle = page switch
        {
            ListPage listPage => listPage.ViewModel?.Title,
            ContentPage contentPage => contentPage.ViewModel?.Title,
            _ => null,
        };

        if (string.IsNullOrEmpty(pageTitle))
        {
            pageTitle = ResourceLoaderInstance.GetString("UntitledPageTitle");
        }

        var announcement = string.Format(CultureInfo.CurrentCulture, _pageNavigatedAnnouncement.Format, pageTitle);

        UIHelper.AnnounceActionForAccessibility(RootFrame, announcement, "CommandPalettePageNavigatedTo");
    }

    /// <summary>
    /// Gets a value indicating whether determines if the current Details have a HeroImage, given the theme
    /// we're currently in. This needs to be evaluated in the view, because the
    /// viewModel doesn't actually know what the current theme is.
    /// </summary>
    public bool HasHeroImage
    {
        get
        {
            var requestedTheme = ActualTheme;
            var iconInfoVM = ViewModel.Details?.HeroImage;
            return iconInfoVM?.HasIcon(requestedTheme == Microsoft.UI.Xaml.ElementTheme.Light) ?? false;
        }
    }

    private void Command_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CommandViewModel commandViewModel)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(commandViewModel.Model));
        }
    }

    private static void ShellPage_OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();

        switch (e.Key)
        {
            case VirtualKey.Left when modifiers.OnlyAlt: // Alt+Left arrow
                WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
                e.Handled = true;
                break;
            case VirtualKey.Home when modifiers.OnlyAlt: // Alt+Home
                WeakReferenceMessenger.Default.Send<GoHomeMessage>(new(WithAnimation: false));
                e.Handled = true;
                break;
            case (VirtualKey)188 when modifiers.OnlyCtrl: // Ctrl+,
                WeakReferenceMessenger.Default.Send<OpenSettingsMessage>(new());
                e.Handled = true;
                break;
            case VirtualKey.F when modifiers.OnlyAlt: // Alt+F: toggle filter focus
                ((ShellPage)sender).ToggleFilterFocus();
                e.Handled = true;
                break;
            case VirtualKey.Down when modifiers.None:
            case VirtualKey.Tab when modifiers.None:
                // In a collapsed compact palette, Down/Tab reveals the top-level items so the
                // user can browse and discover them. Only swallow the key when we actually
                // expand; otherwise let it fall through to normal list navigation / focus move.
                if (((ShellPage)sender).TryExpandCollapsedCompact())
                {
                    e.Handled = true;
                }

                break;
            default:
                {
                    // The CommandBar handles item keybindings; skip them while collapsed so a chord can't hit the hidden selection.
                    if (((ShellPage)sender).ItemActionsAllowed)
                    {
                        TryCommandKeybindingMessage msg = new(modifiers.Ctrl, modifiers.Alt, modifiers.Shift, modifiers.Win, e.Key);
                        WeakReferenceMessenger.Default.Send(msg);
                        e.Handled = msg.Handled;
                    }

                    break;
                }
        }
    }

    private void ShellPage_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ItemActionsAllowed && TryHandleItemAction(e))
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
            e.Handled = true;
        }
    }

    private static bool TryHandleItemAction(KeyRoutedEventArgs e)
    {
        var mods = KeyModifiers.GetCurrent();
        switch (e.Key)
        {
            // Ctrl+Enter
            case VirtualKey.Enter when mods.OnlyCtrl:
                WeakReferenceMessenger.Default.Send<ActivateSecondaryCommandMessage>();
                break;

            // Enter
            case VirtualKey.Enter when mods.None:
                WeakReferenceMessenger.Default.Send<ActivateSelectedListItemMessage>();
                break;

            // Ctrl+K
            case VirtualKey.K when mods.OnlyCtrl:
                WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new(null, null, null, ContextMenuFilterLocation.Bottom));
                break;
            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    private void ShellPage_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            var ptr = e.Pointer;
            if (ptr.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var ptrPt = e.GetCurrentPoint(this);
                if (ptrPt.Properties.IsXButton1Pressed)
                {
                    WeakReferenceMessenger.Default.Send(new NavigateBackMessage());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error handling mouse button press event", ex);
        }
    }

    public void Receive(ExpandCompactModeMessage message)
    {
        // Re-evaluate from the current authoritative page state rather than applying the
        // message's snapshot directly. The message can race with navigation: following a
        // 1-character alias clears the home search (sending a "collapse") right as we
        // navigate to a nested page that must stay expanded. Recomputing here keeps the
        // final state consistent regardless of message/navigation ordering.
        this.DispatcherQueue.TryEnqueue(UpdateCompactModeForCurrentPage);
    }

    private void OnSettingsChanged(ISettingsService sender, SettingsModel args)
    {
        // Only the compact-mode setting affects the expanded/collapsed layout, so ignore
        // hot-reloads that leave it unchanged. Comparing and updating _compactMode on the UI
        // thread keeps it single-threaded regardless of which thread raises the event.
        var compactMode = args.CompactMode;
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (compactMode == _compactMode)
            {
                return;
            }

            _compactMode = compactMode;
            UpdateCompactModeForCurrentPage();
        });
    }

    private void HandleExpandCompactOnUiThread(bool expanded)
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        var newExpanded = settings.CompactMode ? expanded : true;

        // Going from collapsed to expanded realizes the (previously collapsed) content
        // page for the first time, which fires its deferred Loaded event. Suppress the
        // resulting focus/select so we don't select-all the character the user just typed.
        if (!this.ExpandedMode && newExpanded)
        {
            _suppressSelectOnNextLoad = true;
        }

        this.ExpandedMode = newExpanded;
        PropertyChanged?.Invoke(this, new(nameof(ExpandedMode)));
    }

    /// <summary>
    /// Expands a collapsed compact palette on demand (via Down/Tab) so the user can browse the
    /// top-level items. Returns <see langword="false"/> and does nothing unless compact mode is
    /// on and the palette is currently collapsed, letting the caller keep the key's normal
    /// meaning (list navigation / focus traversal) in every other case.
    /// </summary>
    private bool TryExpandCollapsedCompact()
    {
        if (!_compactMode || ExpandedMode)
        {
            return false;
        }

        HandleExpandCompactOnUiThread(true);
        return true;
    }

    /// <summary>
    /// Forces the shell into its compact (collapsed) layout and flushes layout so the host can
    /// read the resulting card height. Only has an effect when compact mode is enabled.
    /// </summary>
    public void EnsureCompactLayout()
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        if (!settings.CompactMode)
        {
            return;
        }

        this.ExpandedMode = false;
        PropertyChanged?.Invoke(this, new(nameof(ExpandedMode)));
        this.UpdateLayout();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _settingsService.SettingsChanged -= OnSettingsChanged;

        if (_hostWindow is not null)
        {
            _hostWindow.IsVisibleToUserChanged -= HostWindow_IsVisibleToUserChanged;
            _hostWindow = null;
        }

        _focusAfterLoadedCts?.Cancel();
        _focusAfterLoadedCts?.Dispose();
        _focusAfterLoadedCts = null;

        var dockWindowManager = _dockWindowManager;
        _dockWindowManager = null;
        dockWindowManager?.Dispose();

        GC.SuppressFinalize(this);
    }
}
