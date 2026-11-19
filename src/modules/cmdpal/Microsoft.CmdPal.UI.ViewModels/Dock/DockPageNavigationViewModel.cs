// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// The page stack of one dock flyout. Page state is only read and changed on the UI
/// scheduler passed to the constructor.
/// </summary>
public sealed partial class DockPageNavigationViewModel : ObservableObject, IDisposable
{
    private readonly TaskScheduler _scheduler;
    private readonly PageNavigationService _pageNavigation;
    private readonly List<PageViewModel> _pages = [];
    private readonly Dictionary<PageViewModel, Task<bool>> _initializationTasks = [];
    private readonly Lock _initializationLock = new();
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private PageViewModel? _currentPage;
    private bool _isDisposed;

    public DockCommandRoute Route { get; }

    public PageViewModel? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(BackStackDepth));
            }
        }
    }

    public bool CanGoBack => _pages.Count > 1;

    public int BackStackDepth => Math.Max(0, _pages.Count - 1);

    /// <summary>
    /// Raised on the UI scheduler when a command result asks to dismiss the flyout.
    /// </summary>
    public event EventHandler? CloseRequested;

    public DockPageNavigationViewModel(
        DockCommandRoute route,
        TaskScheduler scheduler,
        IPageViewModelFactoryService pageFactory,
        IAppHostService appHostService)
    {
        Route = route;
        _scheduler = scheduler;
        _pageNavigation = new(pageFactory, appHostService);
    }

    public bool OwnsSourcePage(PageViewModel? sourcePage) =>
        sourcePage is not null &&
        sourcePage.DockRoute == Route &&
        CurrentPage?.OwnsCommandSource(sourcePage) == true;

    /// <summary>
    /// Pushes the page in <paramref name="message"/>. The first page can come from any
    /// sender on this route; after that, only the current page can push another one.
    /// </summary>
    /// <returns>True when the page was pushed and initialized.</returns>
    public async Task<bool> NavigateAsync(PerformCommandMessage message, CancellationToken cancellationToken = default)
    {
        if (_isDisposed || message.DockRoute != Route || !message.Command.IsPage)
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var token = linkedCancellation.Token;

        await _navigationGate.WaitAsync(token);
        Task<bool>? initializationTask = null;
        PageViewModel? preparedPage = null;
        try
        {
            var (canPush, currentPage) = await RunOnUiThreadAsync(
                () => (CanPushPage(message), CurrentPage), token);
            if (!canPush)
            {
                return false;
            }

            preparedPage = await _pageNavigation.PreparePageAsync(message, currentPage, currentPage is not null, token);
            if (preparedPage is null)
            {
                return false;
            }

            initializationTask = await RunOnUiThreadAsync(
                () => CanPushPage(message) ? PushPage(preparedPage, token) : null, token);
        }
        finally
        {
            if (initializationTask is null)
            {
                CleanupPage(preparedPage);
            }

            _navigationGate.Release();
        }

        return initializationTask is not null && await initializationTask;
    }

    public async Task<bool> GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoBack || _isDisposed)
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var token = linkedCancellation.Token;

        await _navigationGate.WaitAsync(token);
        try
        {
            PageViewModel? removed = null;
            await RunOnUiThreadAsync(
                () =>
                {
                    if (_pages.Count <= 1 || _isDisposed)
                    {
                        return;
                    }

                    removed = _pages[^1];
                    _pages.RemoveAt(_pages.Count - 1);
                    CurrentPage = _pages[^1];
                },
                token);

            CleanupPageAfterInitialization(removed);
            return removed is not null;
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    public async Task<bool> GoHomeAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoBack || _isDisposed)
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var token = linkedCancellation.Token;

        await _navigationGate.WaitAsync(token);
        try
        {
            List<PageViewModel> removed = [];
            await RunOnUiThreadAsync(
                () =>
                {
                    if (_pages.Count <= 1 || _isDisposed)
                    {
                        return;
                    }

                    removed.AddRange(_pages.Skip(1));
                    _pages.RemoveRange(1, _pages.Count - 1);
                    CurrentPage = _pages[0];
                },
                token);

            foreach (var page in removed)
            {
                CleanupPageAfterInitialization(page);
            }

            return removed.Count > 0;
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    /// <summary>
    /// Returns whether a result is shell UI (a toast or a confirmation). The shell must
    /// show these even when the dock flyout that caused them has closed.
    /// </summary>
    public static bool IsShellResult(CommandResultKind kind) =>
        kind is CommandResultKind.ShowToast or CommandResultKind.Confirm;

    /// <summary>
    /// Handles a command result produced by <paramref name="sourcePage"/>. Safe to call
    /// from any thread; navigation is applied later on the UI scheduler. The caller must
    /// read the result kind on a worker inside its extension exception boundary.
    /// </summary>
    /// <returns>False when the shell must still handle the result.</returns>
    public bool HandleCommandResult(PageViewModel sourcePage, CommandResultKind kind)
    {
        if (IsShellResult(kind))
        {
            return false;
        }

        // Every other result belongs to this flyout. A result for a closed flyout or a
        // page that is no longer current is dropped, so it can't move the palette.
        _ = Task.Factory.StartNew(
            () => ApplyCommandResult(sourcePage, kind),
            CancellationToken.None,
            TaskCreationOptions.None,
            _scheduler);
        return true;
    }

    private void ApplyCommandResult(PageViewModel sourcePage, CommandResultKind kind)
    {
        if (_isDisposed || !OwnsSourcePage(sourcePage))
        {
            return;
        }

        switch (kind)
        {
            case CommandResultKind.Dismiss:
            case CommandResultKind.Hide:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
            case CommandResultKind.GoHome:
                _ = ObserveNavigationAsync(GoHomeAsync());
                break;
            case CommandResultKind.GoBack:
                if (CanGoBack)
                {
                    _ = ObserveNavigationAsync(GoBackAsync());
                }
                else
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }

                break;
        }
    }

    private bool CanPushPage(PerformCommandMessage message) =>
        !_isDisposed && (CurrentPage is null || OwnsSourcePage(message.Context?.Page));

    private Task<bool> PushPage(PageViewModel pageViewModel, CancellationToken cancellationToken)
    {
        pageViewModel.DockRoute = Route;
        _pages.Add(pageViewModel);
        CurrentPage = pageViewModel;

        var initializationTask = InitializePageAsync(pageViewModel, cancellationToken);
        lock (_initializationLock)
        {
            _initializationTasks[pageViewModel] = initializationTask;
        }

        return initializationTask;
    }

    private static async Task<bool> InitializePageAsync(PageViewModel page, CancellationToken cancellationToken)
    {
        try
        {
            await PageNavigationService.InitializePageAsync(page, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            page.ShowException(ex);
            return false;
        }
    }

    private static async Task ObserveNavigationAsync(Task<bool> navigationTask)
    {
        try
        {
            await navigationTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to navigate a dock page.", ex);
        }
    }

    private Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            action,
            cancellationToken,
            TaskCreationOptions.None,
            _scheduler);
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<T> action, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, _scheduler);

    /// <summary>
    /// Discards <paramref name="page"/>. <see cref="PageViewModel.CleanupAsync"/> stops local
    /// work at once, then releases extension objects on a worker after the page's active
    /// operations finish, so a slow extension can't block the UI thread.
    /// </summary>
    private static void CleanupPage(PageViewModel? page)
    {
        if (page is null)
        {
            return;
        }

        try
        {
            _ = page.CleanupAsync();
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to clean up a dock page.", ex);
        }
    }

    private void CleanupPageAfterInitialization(PageViewModel? page)
    {
        if (page is null)
        {
            return;
        }

        Task<bool>? initializationTask;
        lock (_initializationLock)
        {
            _initializationTasks.Remove(page, out initializationTask);
        }

        if (initializationTask is null || initializationTask.IsCompleted)
        {
            CleanupPage(page);
            return;
        }

        // An extension call that already started must finish before its page is released.
        _ = initializationTask.ContinueWith(
            _ => CleanupPage(page),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            _scheduler);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCancellation.Cancel();

        foreach (var page in _pages)
        {
            CleanupPageAfterInitialization(page);
        }

        _pages.Clear();
        CurrentPage = null;
        CloseRequested = null;
        _lifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
