// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.UI.Models.Events;
using Microsoft.CommandPalette.UI.Models.Messages;
using Microsoft.CommandPalette.UI.Services;
using Microsoft.CommandPalette.UI.Services.Helpers;
using Microsoft.CommandPalette.UI.ViewModels;
using Microsoft.CommandPalette.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Microsoft.CommandPalette.UI.Pages;

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
    INotifyPropertyChanged,
    IDisposable
{
    private readonly ShellViewModel viewModel;
    private readonly SettingsService _settingsService;
    private readonly ILogger logger;
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };
    private readonly SuppressNavigationTransitionInfo _noAnimation = new();

    private readonly CompositeFormat _pageNavigatedAnnouncement;

    public ShellPage(ShellViewModel viewModel, SettingsService settingsService, ILogger logger)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        _settingsService = settingsService;
        this.logger = logger;

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

        // AddHandler(PreviewKeyDownEvent, new KeyEventHandler(ShellPage_OnPreviewKeyDown), true);
        // AddHandler(KeyDownEvent, new KeyEventHandler(ShellPage_OnKeyDown), false);
        // AddHandler(PointerPressedEvent, new PointerEventHandler(ShellPage_OnPointerPressed), true);
        RootFrame.Navigate(typeof(LoadingPage), new AsyncNavigationRequest(viewModel, CancellationToken.None));

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
            return _settingsService.CurrentSettings.DisableAnimations ? _noAnimation : _slideRightTransition;
        }
    }

    public void Receive(NavigateBackMessage message)
    {
        if (RootFrame.CanGoBack)
        {
            if (!message.FromBackspace ||
                _settingsService.CurrentSettings.BackspaceGoesBack)
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

                PowerToysTelemetry.Log.WriteEvent(new DismissedOnEscEvent());
            }
        }
    }

    public void Receive(NavigateToPageMessage message)
    {
        // TODO GH #526 This needs more better locking too
        _ = _queue.TryEnqueue(() =>
        {
            // Also hide our details pane about here, if we had one
            // HideDetails();

            // Navigate to the appropriate host page for that VM
            // RootFrame.Navigate(
            //    message.Page switch
            //    {
            //        ListViewModel => typeof(ListPage),
            //        ContentPageViewModel => typeof(ContentPage),
            //        _ => throw new NotSupportedException(),
            //    },
            //    new AsyncNavigationRequest(message.Page, message.CancellationToken),
            //    message.WithAnimation ? DefaultPageAnimation : _noAnimation);
            // PowerToysTelemetry.Log.WriteEvent(new OpenPageEvent(RootFrame.BackStackDepth, message.Page.Id));
            // if (!viewModel.IsNested)
            // {
            //    // todo BODGY
            //    RootFrame.BackStack.Clear();
            // }
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

    public void Receive(GoBackMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => GoBack(message.WithAnimation, message.FocusSearch));
    }

    private void GoBack(bool withAnimation = true, bool focusSearch = true)
    {
        HideDetails();

        viewModel.CancelNavigation();

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
            viewModel.GoHome();
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
            // don't focus on each step, just at the end
            GoBack(withAnimation, focusSearch: false);
        }

        // focus search box, even if we were already home
        if (focusSearch)
        {
            SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            SearchBox.SelectSearch();
        }
    }

    public void Receive(ShowToastMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // _toast.ShowToast(message.Message);
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
        if (viewModel is not null &&
            viewModel.CurrentPage is not null)
        {
            if (viewModel.CurrentPage.PageContext.TryGetTarget(out var pageContext))
            {
                Task.Factory.StartNew(
                    () =>
                    {
                        // TERRIBLE HACK TODO GH #245
                        // There's weird wacky bugs with debounce currently.
                        if (!viewModel.IsDetailsVisible)
                        {
                            viewModel.Details = message.Details;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHeroImage)));
                            viewModel.IsDetailsVisible = true;
                            return;
                        }

                        // GH #322:
                        // For inexplicable reasons, if you try to change the details too fast,
                        // we'll explode. This seemingly only happens if you change the details
                        // while we're also scrolling a new list view item into view.
                        _debounceTimer.Debounce(
                            () =>
                            {
                                viewModel.Details = message.Details;

                                // Trigger a re-evaluation of whether we have a hero image based on
                                // the current theme
                                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasHeroImage)));
                            },
                            interval: TimeSpan.FromMilliseconds(50),
                            immediate: viewModel.IsDetailsVisible == false);
                        viewModel.IsDetailsVisible = true;
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
        viewModel.Details = null;
        viewModel.IsDetailsVisible = false;
    }

    public void Receive(ClearSearchMessage message) => SearchBox.ClearSearch();

    public void Receive(HotkeySummonMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => SummonOnUiThread(message));
    }

    public void Receive(SettingsWindowClosedMessage message) => _settingsWindow = null;


    private void SummonOnUiThread(HotkeySummonMessage message)
    {
        var commandId = message.CommandId;
        var isRoot = string.IsNullOrEmpty(commandId);
        if (isRoot)
        {
            // If this is the hotkey for the root level, then always show us
            WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));

            // Depending on the settings, either
            // * Go home, or
            // * Select the search text (if we should remain open on this page)
            if (_settingsService.CurrentSettings.AutoGoHomeInterval == TimeSpan.Zero)
            {
                GoHome(false);
            }
            else if (_settingsService.CurrentSettings.HighlightSearchOnActivate)
            {
                // SearchBox.SelectSearch();
            }
        }

        // else
        // {
        //    try
        //    {
        //        // For a hotkey bound to a command, first lookup the
        //        // command from our list of toplevel commands.
        //        var tlcManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        //        var topLevelCommand = tlcManager.LookupCommand(commandId);
        //        if (topLevelCommand is not null)
        //        {
        //            var command = topLevelCommand.CommandViewModel.Model.Unsafe;
        //            var isPage = command is not IInvokableCommand;
        //            // If the bound command is an invokable command, then
        //            // we don't want to open the window at all - we want to
        //            // just do it.
        //            if (isPage)
        //            {
        //                // If we're here, then the bound command was a page
        //                // of some kind. Let's pop the stack, show the window, and navigate to it.
        //                GoHome(false);
        //                WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(message.Hwnd));
        //            }
        //            var msg = topLevelCommand.GetPerformCommandMessage();
        //            msg.WithAnimation = false;
        //            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(msg);
        //            // we can't necessarily SelectSearch() here, because when the page is loaded,
        //            // we'll fetch the SearchText from the page itself, and that'll stomp the
        //            // selection we start now.
        //            // That's probably okay though.
        //        }
        //    }
        //    catch
        //    {
        //    }
        // }
        WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
    }

    private void BackButton_Clicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => WeakReferenceMessenger.Default.Send<NavigateBackMessage>(new());

    [LoggerMessage(Level = LogLevel.Error)]
    partial void Log_ShowConfirmationMessageError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid navigation target: AsyncNavigationRequest.{targetViewModel} must be {pageViewModel}")]
    partial void Log_InvalidNavigationTarget(string targetViewModel, string pageViewModel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unrecognized target for shell navigation: {parameter}")]
    partial void Log_UnrecognizedNavigationTarget(NavigationEventArgs parameter);
}
