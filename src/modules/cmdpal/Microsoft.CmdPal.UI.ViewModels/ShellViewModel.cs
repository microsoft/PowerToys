// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ShellViewModel : ObservableObject,
    IDisposable,
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>,
    IRecipient<WindowHiddenMessage>
{
    public event EventHandler<PageNavigationRequestedEventArgs>? PageNavigationRequested;

    public event EventHandler<ShellNavigationRequestedEventArgs>? GoHomeRequested;

    public event EventHandler<ShellNavigationRequestedEventArgs>? GoBackRequested;

    private readonly IRootPageService _rootPageService;
    private readonly TaskScheduler _scheduler;
    private readonly PageNavigationService _pageNavigation;
    private readonly Lock _invokeLock = new();
    private Task? _handleInvokeTask;

    // Cancellation token source for page loading/navigation operations
    private CancellationTokenSource? _navigationCts;

    [ObservableProperty]
    public partial bool IsLoaded { get; set; } = false;

    [ObservableProperty]
    public partial DetailsViewModel? Details { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsSearchBoxVisible { get; set; } = true;

    private PageViewModel _currentPage;

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            var oldValue = _currentPage;
            if (SetProperty(ref _currentPage, value))
            {
                oldValue.PropertyChanged -= CurrentPage_PropertyChanged;
                value.PropertyChanged += CurrentPage_PropertyChanged;

                // Re-evaluate search-box visibility for the page we're switching to.
                // CurrentPage_PropertyChanged only reacts to a *change* of HasSearchBox, so
                // switching to a page whose HasSearchBox already holds its final value (e.g.
                // navigating back to a list from a ContentPage) would otherwise never restore
                // the search box. Only force it visible here; hiding it on content pages is
                // deliberately deferred (see ShellPage.FocusAfterLoaded) so focus doesn't jump
                // around for screen readers.
                if (value.HasSearchBox)
                {
                    IsSearchBoxVisible = true;
                }

                try
                {
                    // Frame retains the page for Back until its history entry is discarded.
                    oldValue.SuspendForNavigation();
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError(ex.ToString());
                }

                _ = value.ResumeAfterNavigation();
            }
        }
    }

    private void CurrentPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PageViewModel.HasSearchBox))
        {
            IsSearchBoxVisible = CurrentPage.HasSearchBox;
        }
    }

    private CommandViewModel _rootCommand;

    private bool _isNested;
    private bool _currentlyTransient;

    public bool IsNested => _isNested && !_currentlyTransient;

    public bool IsTransient => _currentlyTransient;

    public PageViewModel NullPage { get; private set; }

    public ShellViewModel(
        TaskScheduler scheduler,
        IRootPageService rootPageService,
        IPageViewModelFactoryService pageViewModelFactory,
        IAppHostService appHostService)
    {
        _pageNavigation = new(pageViewModelFactory, appHostService);
        _scheduler = scheduler;
        _rootPageService = rootPageService;

        NullPage = new NullPageViewModel(_scheduler, appHostService.GetDefaultHost());
        _currentPage = new LoadingPageViewModel(null, _scheduler, appHostService.GetDefaultHost());
        _rootCommand = new(null, new(_currentPage));

        // Register to receive messages
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);
        WeakReferenceMessenger.Default.Register<WindowHiddenMessage>(this);
    }

    [RelayCommand]
    public async Task<bool> LoadAsync()
    {
        // First, do any loading that the root page service needs to do before we can
        // display the root page. For example, this might include loading
        // the built-in commands, or loading the settings.
        await _rootPageService.PreLoadAsync();

        IsLoaded = true;

        // Now that the basics are set up, we can load the root page.
        _rootCommand = new(_rootPageService.GetRootPage(), new(CurrentPage));
        _rootCommand.FastInitializeProperties();

        // This sends a message to us to load the root page view model.
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(_rootCommand));

        // Now that the root page is loaded, do any post-load work that the root page service needs to do.
        // This runs asynchronously, on a background thread.
        // This might include starting extensions, for example.
        // Note: We don't await this, so that we can return immediately.
        // This is important because we don't want to block the UI thread.
        _ = Task.Run(async () =>
        {
            await _rootPageService.PostLoadRootPageAsync();
        });

        return true;
    }

    private async Task LoadPageViewModelAsync(PageViewModel viewModel, CancellationToken cancellationToken = default)
    {
        await PageNavigationService.InitializePageAsync(viewModel, cancellationToken).ConfigureAwait(false);
        await Task.Factory.StartNew(
            () => SetCurrentPageAfterLoad(viewModel, cancellationToken),
            cancellationToken,
            TaskCreationOptions.None,
            _scheduler);
    }

    private void SetCurrentPageAfterLoad(PageViewModel viewModel, CancellationToken cancellationToken)
    {
        if (viewModel.IsDiscarded)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (viewModel is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    CoreLogger.LogError(ex.ToString());
                }
            }

            return;
        }

        CurrentPage = viewModel;
    }

    public void Receive(PerformCommandMessage message)
    {
        if (message.DockRoute is not null)
        {
            return;
        }

        _ = PerformCommandAsync(message);
    }

    internal Task PerformCommandAsync(PerformCommandMessage message)
    {
        var command = message.Command.Model.Unsafe;
        if (command is null)
        {
            return Task.CompletedTask;
        }

        // Determine whether this is the root/home page navigation BEFORE
        // computing providerContext. When navigating back to the root page we
        // must use an empty provider context so that home-page list items don't
        // inherit a pinning-capable context left over from the previous sub-page
        // (which can happen e.g. when the window is hidden while on a sub-page).
        // The shared preparation service uses an empty provider context for this page.
        var isMainPage = command == _rootCommand.Model.Unsafe;

        // Only commands sent from the palette's own root page are top-level. Commands from
        // other surfaces (for example, a dock flyout page) must not update home-page history.
        var sourcePage = message.Context?.Page;
        var isTopLevel = CurrentPage.IsRootPage && (sourcePage is null || ReferenceEquals(sourcePage, CurrentPage));

        if (message.Command.IsPage)
        {
            return NavigateToPageAsync(message, isMainPage, isTopLevel);
        }

        var host = _pageNavigation.ResolveHost(message, CurrentPage);
        try
        {
            if (message.Command.IsInvokableCommand)
            {
                CoreLogger.LogDebug($"Invoking command");

                _rootPageService.OnPerformCommand(message.CommandContext, isTopLevel, host);
                WeakReferenceMessenger.Default.Send<TelemetryBeginInvokeMessage>();
                StartInvoke(message, host);
            }
            else
            {
                _rootPageService.OnPerformCommand(message.CommandContext, isTopLevel, host);
            }
        }
        catch (Exception ex)
        {
            // TODO: It would be better to do this as a page exception, rather
            // than a silent log message.
            host?.Log(ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task NavigateToPageAsync(PerformCommandMessage message, bool isMainPage, bool isTopLevel)
    {
        var cancellation = BeginNavigation();
        var token = cancellation.Token;
        PageViewModel? page = null;
        var published = false;
        try
        {
            page = await _pageNavigation.PreparePageAsync(
                message,
                CurrentPage,
                !isMainPage,
                token,
                isMainPage ? CommandProviderContext.Empty : null);
            if (page is null)
            {
                throw new NotSupportedException("Failed to create a page view model.");
            }

            await Task.Factory.StartNew(
                () =>
                {
                    ShowPage(message, page, !isMainPage, isTopLevel, token);
                    published = true;
                },
                token,
                TaskCreationOptions.None,
                _scheduler);

            // Start loading in the background...
            var loadingTask = LoadPageViewModelAsync(page, token);

            // then start navigating the UI
            PageNavigationRequested?.Invoke(
                this,
                new(page, message.WithAnimation, message.TransientPage, token));

            await loadingTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to navigate to a page.", ex);
        }
        finally
        {
            if (!published && page is not null)
            {
                _ = page.CleanupAsync();
            }

            if (Interlocked.CompareExchange(ref _navigationCts, null, cancellation) == cancellation)
            {
                cancellation.Dispose();
            }
        }
    }

    private CancellationTokenSource BeginNavigation()
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _navigationCts, cancellation);
        if (previous is not null)
        {
            try
            {
                previous.Cancel();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError(ex.ToString());
            }
            finally
            {
                previous.Dispose();
            }
        }

        return cancellation;
    }

    private void ShowPage(
        PerformCommandMessage message,
        PageViewModel page,
        bool nested,
        bool isTopLevel,
        CancellationToken cancellationToken)
    {
        page.HasBackButton = nested && !message.TransientPage;
        var host = page.ExtensionHost;
        _rootPageService.OnPerformCommand(message.CommandContext, isTopLevel, host);
        _isNested = nested;
        _currentlyTransient = message.TransientPage;

        if (message.ShowWindowIfPage)
        {
            WeakReferenceMessenger.Default.Send<ShowWindowMessage>(new(IntPtr.Zero));
        }

        var extensionId = host.GetExtensionDisplayName() ?? "builtin";
        WeakReferenceMessenger.Default.Send<TelemetryCommandStartedMessage>();
        WeakReferenceMessenger.Default.Send<TelemetryExtensionInvokedMessage>(
            new(extensionId, message.Command.Id, message.Command.Name, true, 0));
    }

    private void StartInvoke(PerformCommandMessage message, AppExtensionHost? host)
    {
        // TODO GH #525 This needs more better locking.
        lock (_invokeLock)
        {
            if (_handleInvokeTask is not null)
            {
                // do nothing - a command is already doing a thing
            }
            else
            {
                // Count only accepted invocations, before Invoke can hide the palette and end the session.
                WeakReferenceMessenger.Default.Send<TelemetryCommandStartedMessage>();
                _handleInvokeTask = Task.Run(() =>
                {
                    SafeHandleInvokeCommandSynchronous(message, host);
                });
            }
        }
    }

    private void SafeHandleInvokeCommandSynchronous(PerformCommandMessage message, AppExtensionHost? host)
    {
        // Telemetry: Track command execution time and success
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var extensionId = "builtin";
        var commandId = "unknown";
        var commandName = "unknown";
        var success = false;

        try
        {
            ICommandResult? result;
            try
            {
                extensionId = host?.GetExtensionDisplayName() ?? "builtin";
                commandId = message.Command.Id;
                commandName = message.Command.Name;

                // Call out to extension process.
                // * May fail!
                // * May never return!
                result = ((IInvokableCommand)message.Command.Model.Unsafe!).Invoke(message.CommandContext);
                success = true;
            }
            finally
            {
                // Report the invocation outcome before processing its result.
                stopwatch.Stop();
                WeakReferenceMessenger.Default.Send<TelemetryExtensionInvokedMessage>(
                    new(extensionId, commandId, commandName, success, (ulong)stopwatch.ElapsedMilliseconds));
            }

            // But if it did succeed, we need to handle the result.
            UnsafeHandleCommandResult(
                result,
                message.OnBeforeShowConfirmation,
                message.ResultHandler,
                message.Context?.Page);
        }
        catch (Exception ex)
        {
            // Telemetry: Track errors for session metrics
            WeakReferenceMessenger.Default.Send<ErrorOccurredMessage>(new());

            // TODO: It would be better to do this as a page exception, rather
            // than a silent log message.
            host?.Log(ex.Message);
        }
        finally
        {
            lock (_invokeLock)
            {
                _handleInvokeTask = null;
            }
        }
    }

    private void UnsafeHandleCommandResult(
        ICommandResult? result,
        Action? onBeforeShowConfirmation = null,
        Func<ICommandResult, bool>? resultHandler = null,
        PageViewModel? sourcePage = null)
    {
        if (result is null)
        {
            // No result, nothing to do.
            return;
        }

        var kind = result.Kind;
        CoreLogger.LogDebug($"handling {kind.ToString()}");

        WeakReferenceMessenger.Default.Send<TelemetryInvokeResultMessage>(new(kind));
        if (resultHandler?.Invoke(result) == true)
        {
            return;
        }

        switch (kind)
        {
            case CommandResultKind.Dismiss:
                {
                    // Reset the palette to the main page and dismiss
                    GoHome(withAnimation: false, focusSearch: false);
                    WeakReferenceMessenger.Default.Send(new DismissMessage());
                    break;
                }

            case CommandResultKind.GoHome:
                {
                    // Go back to the main page, but keep it open
                    GoHome();
                    break;
                }

            case CommandResultKind.GoBack:
                {
                    GoBack();
                    break;
                }

            case CommandResultKind.Hide:
                {
                    // Keep this page open, but hide the palette.
                    WeakReferenceMessenger.Default.Send(new DismissMessage());
                    break;
                }

            case CommandResultKind.KeepOpen:
                {
                    // Do nothing.
                    break;
                }

            case CommandResultKind.Confirm:
                {
                    if (result.Args is IConfirmationArgs a)
                    {
                        // Give the original sender (e.g. the dock) a chance to
                        // prepare UI before the confirmation dialog surfaces.
                        try
                        {
                            onBeforeShowConfirmation?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            CoreLogger.LogError(ex.ToString());
                        }

                        WeakReferenceMessenger.Default.Send<ShowConfirmationMessage>(new(a));
                    }

                    break;
                }

            case CommandResultKind.ShowToast:
                {
                    if (result.Args is IToastArgs a)
                    {
                        // Extensions built against newer SDKs can attach an icon
                        // and an action command via IToastArgs2.
                        IconInfoViewModel? icon = null;
                        CommandViewModel? command = null;
                        if (a is IToastArgs2 a2)
                        {
                            if (a2.Icon is not null)
                            {
                                icon = new IconInfoViewModel(a2.Icon);
                                icon.InitializeProperties();
                            }

                            var toastCommand = a2.Command;
                            if (toastCommand is not null)
                            {
                                command = new CommandViewModel(toastCommand, new(sourcePage ?? CurrentPage));
                                command.InitializeProperties();
                            }
                        }

                        WeakReferenceMessenger.Default.Send<ShowToastMessage>(new(a.Message, icon, command));
                        UnsafeHandleCommandResult(a.Result, onBeforeShowConfirmation, resultHandler, sourcePage);
                    }

                    break;
                }
        }
    }

    public void GoHome(bool withAnimation = true, bool focusSearch = true)
    {
        _rootPageService.GoHome();
        GoHomeRequested?.Invoke(this, new(withAnimation, focusSearch));
    }

    /// <summary>
    /// Resets navigation to the root page, clearing any transient state.
    /// Use when entering from a hotkey while the palette may already be
    /// showing a transient dock page.
    /// </summary>
    public void ResetToHome()
    {
        _currentlyTransient = false;
        _rootPageService.GoHome();
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(_rootCommand));
    }

    public void GoBack(bool withAnimation = true, bool focusSearch = true)
    {
        GoBackRequested?.Invoke(this, new(withAnimation, focusSearch));
    }

    public void Receive(HandleCommandResultMessage message)
    {
        if (message.DockRoute is not null)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                UnsafeHandleCommandResult(
                    message.Result.Unsafe,
                    message.OnBeforeShowConfirmation,
                    message.ResultHandler,
                    message.Context?.Page);
            }
            catch (Exception ex)
            {
                CoreLogger.LogError("Failed to handle a command result.", ex);
                message.Context?.Page?.ShowException(ex);
            }
        });
    }

    public void Receive(WindowHiddenMessage message)
    {
        // If the window was hidden while we had a transient page, we need to reset that state.
        if (_currentlyTransient)
        {
            _currentlyTransient = false;

            // navigate back to the main page without animation
            GoHome(withAnimation: false, focusSearch: false);
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(_rootCommand));
        }
    }

    public void CancelNavigation()
    {
        _navigationCts?.Cancel();
    }

    public void Dispose()
    {
        CancelNavigation();
        _handleInvokeTask?.Dispose();
        _navigationCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
