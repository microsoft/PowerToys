// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ShellViewModel : ObservableObject,
    IDisposable,
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>,
    IRecipient<WindowHiddenMessage>
{
    private readonly IRootPageService _rootPageService;
    private readonly IAppHostService _appHostService;
    private readonly TaskScheduler _scheduler;
    private readonly IPageViewModelFactoryService _pageViewModelFactory;
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

                if (oldValue is IDisposable disposable)
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

    private IPage? _rootPage;

    private bool _isNested;
    private bool _currentlyTransient;

    public bool IsNested => _isNested && !_currentlyTransient;

    public PageViewModel NullPage { get; private set; }

    public ShellViewModel(
        TaskScheduler scheduler,
        IRootPageService rootPageService,
        IPageViewModelFactoryService pageViewModelFactory,
        IAppHostService appHostService)
    {
        _pageViewModelFactory = pageViewModelFactory;
        _scheduler = scheduler;
        _rootPageService = rootPageService;
        _appHostService = appHostService;

        NullPage = new NullPageViewModel(_scheduler, appHostService.GetDefaultHost());
        NullPage.HasBackButton = false;
        _currentPage = new LoadingPageViewModel(null, _scheduler, appHostService.GetDefaultHost());

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
        _rootPage = _rootPageService.GetRootPage();

        // This sends a message to us to load the root page view model.
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(new ExtensionObject<ICommand>(_rootPage)));

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

    private async Task ShowPageAsFlyoutAsync(PageViewModel viewModel, object flyoutUiContext, CancellationToken cancellationToken = default)
    {
        if (viewModel is not IContextMenuContext ctx)
        {
            return;
        }

        // Instead of doing normal page navigation, we want to open this
        // page as a flyout anchored to the given position.
        var initialized = await InitializePageViewModelAsync(viewModel, cancellationToken);
        if (initialized)
        {
            await SetCurrentPageAsync(viewModel, cancellationToken);

            // send message
            WeakReferenceMessenger.Default.Send(new ShowCommandInContextMenuMessage(ctx, flyoutUiContext));

            //// now cleanup navigation
            // await CleanupNavigationTokenAsync(cancellationToken);
        }
    }

    private async Task LoadPageViewModelAsync(PageViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var initialized = await InitializePageViewModelAsync(viewModel, cancellationToken);
        if (initialized)
        {
            await SetCurrentPageAsync(viewModel, cancellationToken);
        }
    }

    /// <summary>
    /// Runs the async initialization for a page view model on a background thread.
    /// Returns true if the page is ready to be displayed, false otherwise.
    /// </summary>
    private async Task<bool> InitializePageViewModelAsync(PageViewModel viewModel, CancellationToken cancellationToken)
    {
        if (viewModel.IsInitialized
            || viewModel.InitializeCommand is null)
        {
            // Already initialized (or nothing to do), ready to display.
            return true;
        }

        var success = false;
        await Task.Run(
            async () =>
            {
                viewModel.InitializeCommand.Execute(null);
                await viewModel.InitializeCommand.ExecutionTask!;

                if (viewModel.InitializeCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
                {
                    if (viewModel.InitializeCommand.ExecutionTask.Exception is AggregateException ex)
                    {
                        CoreLogger.LogError(ex.ToString());
                    }
                }
                else
                {
                    success = true;
                }
            },
            cancellationToken);

        return success;
    }

    /// <summary>
    /// Hops to the UI thread to set <see cref="CurrentPage"/>, or disposes the
    /// view model if the operation was cancelled.
    /// </summary>
    private async Task SetCurrentPageAsync(PageViewModel viewModel, CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(
            () =>
            {
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
            },
            cancellationToken,
            TaskCreationOptions.None,
            _scheduler);
    }

    private async Task CleanupNavigationTokenAsync(CancellationTokenSource cts)
    {
        // clean up the navigation token if it's still ours
        if (Interlocked.CompareExchange(ref _navigationCts, null, cts) == cts)
        {
            cts.Dispose();
        }
    }

    public void Receive(PerformCommandMessage message)
    {
        PerformCommand(message);
    }

    private void PerformCommand(PerformCommandMessage message)
    {
        // Create/replace the navigation cancellation token.
        // If one already exists, cancel and dispose it first.
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _navigationCts, newCts);
        if (oldCts is not null)
        {
            try
            {
                oldCts.Cancel();
            }
            catch (Exception ex)
            {
                CoreLogger.LogError(ex.ToString());
            }
            finally
            {
                oldCts.Dispose();
            }
        }

        var navigationToken = newCts.Token;

        var command = message.Command.Unsafe;
        if (command is null)
        {
            return;
        }

        var host = _appHostService.GetHostForCommand(message.Context, CurrentPage.ExtensionHost);

        _rootPageService.OnPerformCommand(message.Context, CurrentPage.IsRootPage, host);

        try
        {
            if (command is IPage page)
            {
                CoreLogger.LogDebug($"Navigating to page");
                StartOpenPage(message, page, host, newCts, navigationToken);
            }
            else if (command is IInvokableCommand invokable)
            {
                CoreLogger.LogDebug($"Invoking command");

                WeakReferenceMessenger.Default.Send<TelemetryBeginInvokeMessage>();
                StartInvoke(message, invokable, host);
            }
        }
        catch (Exception ex)
        {
            // TODO: It would be better to do this as a page exception, rather
            // than a silent log message.
            host?.Log(ex.Message);
        }
    }

    private void StartInvoke(PerformCommandMessage message, IInvokableCommand invokable, AppExtensionHost? host)
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
                _handleInvokeTask = Task.Run(() =>
                {
                    SafeHandleInvokeCommandSynchronous(message, invokable, host);
                });
            }
        }
    }

    private void SafeHandleInvokeCommandSynchronous(PerformCommandMessage message, IInvokableCommand invokable, AppExtensionHost? host)
    {
        // Telemetry: Track command execution time and success
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var command = message.Command.Unsafe;
        var extensionId = host?.GetExtensionDisplayName() ?? "builtin";
        var commandId = command?.Id ?? "unknown";
        var commandName = command?.Name ?? "unknown";
        var success = false;

        try
        {
            // Call out to extension process.
            // * May fail!
            // * May never return!
            var result = invokable.Invoke(message.Context);

            // But if it did succeed, we need to handle the result.
            UnsafeHandleCommandResult(result);

            success = true;
            _handleInvokeTask = null;
        }
        catch (Exception ex)
        {
            success = false;
            _handleInvokeTask = null;

            // Telemetry: Track errors for session metrics
            WeakReferenceMessenger.Default.Send<ErrorOccurredMessage>(new());

            // TODO: It would be better to do this as a page exception, rather
            // than a silent log message.
            host?.Log(ex.Message);
        }
        finally
        {
            // Telemetry: Send extension invocation metrics (always sent, even on failure)
            stopwatch.Stop();
            WeakReferenceMessenger.Default.Send<TelemetryExtensionInvokedMessage>(
                new(extensionId, commandId, commandName, success, (ulong)stopwatch.ElapsedMilliseconds));
        }
    }

    private void UnsafeHandleCommandResult(ICommandResult? result)
    {
        if (result is null)
        {
            // No result, nothing to do.
            return;
        }

        var kind = result.Kind;
        CoreLogger.LogDebug($"handling {kind.ToString()}");

        WeakReferenceMessenger.Default.Send<TelemetryInvokeResultMessage>(new(kind));
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
                        WeakReferenceMessenger.Default.Send<ShowConfirmationMessage>(new(a));
                    }

                    break;
                }

            case CommandResultKind.ShowToast:
                {
                    if (result.Args is IToastArgs a)
                    {
                        WeakReferenceMessenger.Default.Send<ShowToastMessage>(new(a.Message));
                        UnsafeHandleCommandResult(a.Result);
                    }

                    break;
                }
        }
    }

    private void StartOpenPage(PerformCommandMessage message, IPage page, AppExtensionHost? host, CancellationTokenSource cts, CancellationToken navigationToken)
    {
        var isMainPage = page == _rootPage;

        // Telemetry: Track extension page navigation for session metrics
        // TODO! this block is unsafe
        if (host is not null)
        {
            var extensionId = host.GetExtensionDisplayName() ?? "builtin";
            var commandId = page.Id ?? "unknown";
            var commandName = page.Name ?? "unknown";
            WeakReferenceMessenger.Default.Send<TelemetryExtensionInvokedMessage>(
                new(extensionId, commandId, commandName, true, 0));
        }

        // Construct our ViewModel of the appropriate type and pass it the UI Thread context.
        var pageViewModel = _pageViewModelFactory.TryCreatePageViewModel(page, !isMainPage, host!);
        if (pageViewModel is null)
        {
            CoreLogger.LogError($"Failed to create ViewModel for page {page.GetType().Name}");
            throw new NotSupportedException();
        }

        if (pageViewModel is ListViewModel listViewModel
            && message.OpenAsFlyout
            && message.FlyoutUiContext is not null)
        {
            ShowPageAsFlyoutAsync(listViewModel, message.FlyoutUiContext, navigationToken)
                .ContinueWith(
                    (Task t) => CleanupNavigationTokenAsync(cts),
                    navigationToken,
                    TaskContinuationOptions.None,
                    _scheduler);
            return;
        }

        // -------------------------------------------------------------
        // Slice it here.
        // Stuff above this, we need to always do, for both commands in the palette and flyout items
        //
        // Below here, this is all specific to navigating the current page of the palette
        _isNested = !isMainPage;
        _currentlyTransient = message.TransientPage;

        pageViewModel.IsRootPage = isMainPage;
        pageViewModel.HasBackButton = IsNested;

        // Clear command bar, ViewModel initialization can already set new commands if it wants to
        OnUIThread(() => WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(null)));

        // Kick off async loading of our ViewModel
        LoadPageViewModelAsync(pageViewModel, navigationToken)
            .ContinueWith(
                (Task t) => CleanupNavigationTokenAsync(cts),
                navigationToken,
                TaskContinuationOptions.None,
                _scheduler);

        // While we're loading in the background, immediately move to the next page.
        NavigateToPageMessage msg = new(pageViewModel, message.WithAnimation, navigationToken, message.TransientPage);
        WeakReferenceMessenger.Default.Send(msg);

        // Note: Originally we set our page back in the ViewModel here, but that now happens in response to the Frame navigating triggered from the above
        // See RootFrame_Navigated event handler.
    }

    public void GoHome(bool withAnimation = true, bool focusSearch = true)
    {
        _rootPageService.GoHome();
        WeakReferenceMessenger.Default.Send<GoHomeMessage>(new(withAnimation, focusSearch));
    }

    public void GoBack(bool withAnimation = true, bool focusSearch = true)
    {
        WeakReferenceMessenger.Default.Send<GoBackMessage>(new(withAnimation, focusSearch));
    }

    public void Receive(HandleCommandResultMessage message)
    {
        UnsafeHandleCommandResult(message.Result.Unsafe);
    }

    public void Receive(WindowHiddenMessage message)
    {
        // If the window was hidden while we had a transient page, we need to reset that state.
        if (_currentlyTransient)
        {
            _currentlyTransient = false;

            // navigate back to the main page without animation
            GoHome(withAnimation: false, focusSearch: false);
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(new ExtensionObject<ICommand>(_rootPage)));
        }
    }

    private void OnUIThread(Action action)
    {
        _ = Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            _scheduler);
    }

    public void CancelNavigation()
    {
        _navigationCts?.Cancel();
    }

    public void Dispose()
    {
        _handleInvokeTask?.Dispose();
        _navigationCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
