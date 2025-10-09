// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Settings;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.Core;
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
    INotifyPropertyChanged
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };
    private readonly SuppressNavigationTransitionInfo _noAnimation = new();

    private readonly ToastWindow _toast = new();

    private readonly CompositeFormat _pageNavigatedAnnouncement;

    private SettingsWindow? _settingsWindow;

    public ShellViewModel ViewModel { get; private set; } = App.Current.Services.GetService<ShellViewModel>()!;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ShellPage()
    {
        this.InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.Register<NavigateBackMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this);
        WeakReferenceMessenger.Default.Register<HotkeySummonMessage>(this);
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

        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(ShellPage_OnPreviewKeyDown), true);
        AddHandler(KeyDownEvent, new KeyEventHandler(ShellPage_OnKeyDown), false);
        AddHandler(PointerPressedEvent, new PointerEventHandler(ShellPage_OnPointerPressed), true);

        RootFrame.Navigate(typeof(LoadingPage), new AsyncNavigationRequest(ViewModel, CancellationToken.None));

        var pageAnnouncementFormat = ResourceLoaderInstance.GetString("ScreenReader_Announcement_NavigatedToPage0");
        _pageNavigatedAnnouncement = CompositeFormat.Parse(pageAnnouncementFormat);
    }

    /// <summary>
    /// Gets the default page animation, depending on the settings
    /// </summary>
    private NavigationTransitionInfo DefaultPageAnimation
    {
        get
        {
            var settings = App.Current.Services.GetService<SettingsModel>()!;
            return settings.DisableAnimations ? _noAnimation : _slideRightTransition;
        }
    }

    public void Receive(NavigateBackMessage message)
    {
        var settings = App.Current.Services.GetService<SettingsModel>()!;

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
                WeakReferenceMessenger.Default.Send<DismissMessage>();

                PowerToysTelemetry.Log.WriteEvent(new CmdPalDismissedOnEsc());
            }
        }
    }

    public void Receive(NavigateToPageMessage message)
    {
        // TODO GH #526 This needs more better locking too
        _ = _queue.TryEnqueue(() =>
        {
            // Also hide our details pane about here, if we had one
            HideDetails();

            // Navigate to the appropriate host page for that VM
            RootFrame.Navigate(
                message.Page switch
                {
                    ListViewModel => typeof(ListPage),
                    ContentPageViewModel => typeof(ContentPage),
                    _ => throw new NotSupportedException(),
                },
                new AsyncNavigationRequest(message.Page, message.CancellationToken),
                message.WithAnimation ? DefaultPageAnimation : _noAnimation);

            PowerToysTelemetry.Log.WriteEvent(new OpenPage(RootFrame.BackStackDepth));

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
            _toast.ShowToast(message.Message);
        });
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

    private void InitializeConfirmationDialog(ConfirmResultViewModel vm)
    {
        vm.SafeInitializePropertiesSynchronous();
    }

    public void Receive(OpenSettingsMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            OpenSettings();
        });
    }

    public void OpenSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
        }

        _settingsWindow.Activate();
        _settingsWindow.BringToFront();
    }

    public void Receive(ShowDetailsMessage message)
    {
        if (ViewModel is not null &&
            ViewModel.CurrentPage is not null)
        {
            if (ViewModel.CurrentPage.PageContext.TryGetTarget(out var pageContext))
            {
                Task.Factory.StartNew(
                    () =>
                    {
                        // TERRIBLE HACK TODO GH #245
                        // There's weird wacky bugs with debounce currently.
                        if (!ViewModel.IsDetailsVisible)
                        {
                            ViewModel.Details = message.Details;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHeroImage)));
                            ViewModel.IsDetailsVisible = true;
                            return;
                        }

                        // GH #322:
                        // For inexplicable reasons, if you try to change the details too fast,
                        // we'll explode. This seemingly only happens if you change the details
                        // while we're also scrolling a new list view item into view.
                        _debounceTimer.Debounce(
                            () =>
                            {
                                ViewModel.Details = message.Details;

                                // Trigger a re-evaluation of whether we have a hero image based on
                                // the current theme
                                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHeroImage)));
                            },
                            interval: TimeSpan.FromMilliseconds(50),
                            immediate: ViewModel.IsDetailsVisible == false);
                        ViewModel.IsDetailsVisible = true;
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    pageContext.Scheduler);
            }
        }
    }

    public void Receive(HideDetailsMessage message) => HideDetails();

    public void Receive(LaunchUriMessage message) => _ = global::Windows.System.Launcher.LaunchUriAsync(message.Uri);

    private void HideDetails()
    {
        ViewModel.Details = null;
        ViewModel.IsDetailsVisible = false;
    }

    public void Receive(ClearSearchMessage message) => SearchBox.ClearSearch();

    public void Receive(HotkeySummonMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => SummonOnUiThread(message));
    }

    public void Receive(SettingsWindowClosedMessage message) => _settingsWindow = null;

    private void SummonOnUiThread(HotkeySummonMessage message)
    {
        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var commandId = message.CommandId;
        var isRoot = string.IsNullOrEmpty(commandId);
        if (isRoot)
        {
            // If this is the hotkey for the root level, then always show us
            WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));

            // Depending on the settings, either
            // * Go home, or
            // * Select the search text (if we should remain open on this page)
            if (settings.HotkeyGoesHome)
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
                        // of some kind. Let's pop the stack, show the window, and navigate to it.
                        GoHome(false);

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

        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    public void Receive(GoBackMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => GoBack(message.WithAnimation, message.FocusSearch));
    }

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

        if (!RootFrame.CanGoBack)
        {
            ViewModel.GoHome();
        }

        if (focusSearch)
        {
            SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            SearchBox.SelectSearch();
        }
    }

    public void Receive(GoHomeMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => GoHome(withAnimation: message.WithAnimation, focusSearch: message.FocusSearch));
    }

    private void GoHome(bool withAnimation = true, bool focusSearch = true)
    {
        while (RootFrame.CanGoBack)
        {
            GoBack(withAnimation, focusSearch);
        }
    }

    private void BackButton_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());

    private void RootFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
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
            element.Loaded += FocusAfterLoaded;
        }
    }

    private void FocusAfterLoaded(object sender, RoutedEventArgs e)
    {
        var page = (Page)sender;
        page.Loaded -= FocusAfterLoaded;

        AnnounceNavigationToPage(page);

        var shouldSearchBoxBeVisible = ViewModel.CurrentPage?.HasSearchBox ?? false;

        if (shouldSearchBoxBeVisible || page is not ContentPage)
        {
            ViewModel.IsSearchBoxVisible = shouldSearchBoxBeVisible;
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.SelectSearch();
        }
        else
        {
            _ = Task.Run(async () =>
            {
                await page.DispatcherQueue.EnqueueAsync(async () =>
                {
                    // I hate this so much, but it can take a while for the page to be ready to accept focus;
                    // focusing page with MarkdownTextBlock takes up to 5 attempts (* 100ms delay between attempts)
                    for (var i = 0; i < 10; i++)
                    {
                        if (FocusManager.FindFirstFocusableElement(page) is FrameworkElement frameworkElement)
                        {
                            var set = frameworkElement.Focus(FocusState.Programmatic);
                            if (set)
                            {
                                break;
                            }
                        }

                        await Task.Delay(100);
                    }

                    // Update the search box visibility based on the current page:
                    // - We do this here after navigation so the focus is not jumping around too much,
                    //   it messes with screen readers if we do it too early
                    // - Since this should hide the search box on content pages, it's not a problem if we
                    //   wait for the code above to finish trying to focus the content
                    ViewModel.IsSearchBoxVisible = ViewModel.CurrentPage?.HasSearchBox ?? false;
                });
            });
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
        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        var altPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
        var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var winPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(CoreVirtualKeyStates.Down) ||
                         InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(CoreVirtualKeyStates.Down);

        var onlyAlt = altPressed && !ctrlPressed && !shiftPressed && !winPressed;
        if (e.Key == VirtualKey.Left && onlyAlt)
        {
            WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Home && onlyAlt)
        {
            WeakReferenceMessenger.Default.Send<GoHomeMessage>(new(WithAnimation: false));
            e.Handled = true;
        }
        else
        {
            // The CommandBar is responsible for handling all the item keybindings,
            // since the bound context item may need to then show another
            // context menu
            TryCommandKeybindingMessage msg = new(ctrlPressed, altPressed, shiftPressed, winPressed, e.Key);
            WeakReferenceMessenger.Default.Send(msg);
            e.Handled = msg.Handled;
        }
    }

    private static void ShellPage_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (ctrlPressed && e.Key == VirtualKey.Enter)
        {
            // ctrl+enter
            WeakReferenceMessenger.Default.Send<ActivateSecondaryCommandMessage>();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            WeakReferenceMessenger.Default.Send<ActivateSelectedListItemMessage>();
            e.Handled = true;
        }
        else if (ctrlPressed && e.Key == VirtualKey.K)
        {
            // ctrl+k
            WeakReferenceMessenger.Default.Send<OpenContextMenuMessage>(new OpenContextMenuMessage(null, null, null, ContextMenuFilterLocation.Bottom));
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());
            e.Handled = true;
        }
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
}
