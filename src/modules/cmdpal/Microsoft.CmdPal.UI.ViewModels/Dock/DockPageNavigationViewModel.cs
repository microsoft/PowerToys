// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockPageNavigationViewModel : ObservableObject, IDisposable
{
    private readonly TaskScheduler _scheduler;
    private readonly IPageViewModelFactoryService _pageFactory;
    private readonly IAppHostService _appHostService;
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

    public bool OwnsSourcePage(PageViewModel? sourcePage) =>
        sourcePage?.DockRoute == Route && CurrentPage?.OwnsCommandSource(sourcePage) == true;

    public DockPageNavigationViewModel(
        DockCommandRoute route,
        TaskScheduler scheduler,
        IPageViewModelFactoryService pageFactory,
        IAppHostService appHostService)
    {
        Route = route;
        _scheduler = scheduler;
        _pageFactory = pageFactory;
        _appHostService = appHostService;
    }

    public async Task<bool> NavigateAsync(PerformCommandMessage message, CancellationToken cancellationToken = default)
    {
        if (message.DockRoute != Route || message.Command.Unsafe is not IPage page)
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        var token = linkedCancellation.Token;

        await _navigationGate.WaitAsync(token);
        Task<bool>? initializationTask = null;
        try
        {
            if (_isDisposed)
            {
                return false;
            }

            var currentHost = message.SourceExtensionHost ?? CurrentPage?.ExtensionHost;
            var currentProviderContext = message.SourceProviderContext ?? CurrentPage?.ProviderContext;
            var host = _appHostService.GetHostForCommand(message.Context, currentHost);
            var providerContext = _appHostService.GetProviderContextForCommand(message.Context, currentProviderContext);
            var nested = _pages.Count > 0;
            var pageViewModel = _pageFactory.TryCreatePageViewModel(page, nested, host, providerContext);
            if (pageViewModel is null)
            {
                return false;
            }

            pageViewModel.DockRoute = Route;
            pageViewModel.IsRootPage = !nested;
            pageViewModel.HasBackButton = nested;

            try
            {
                await RunOnUiThreadAsync(
                    () =>
                    {
                        _pages.Add(pageViewModel);
                        CurrentPage = pageViewModel;
                        initializationTask = InitializePageAsync(pageViewModel, token);
                        lock (_initializationLock)
                        {
                            _initializationTasks[pageViewModel] = initializationTask;
                        }
                    },
                    token);
            }
            catch
            {
                CleanupPage(pageViewModel);
                throw;
            }
        }
        finally
        {
            _navigationGate.Release();
        }

        return await initializationTask!;
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
            if (_pages.Count <= 1 || _isDisposed)
            {
                return false;
            }

            PageViewModel? removed = null;
            await RunOnUiThreadAsync(
                () =>
                {
                    removed = _pages[^1];
                    _pages.RemoveAt(_pages.Count - 1);
                    CurrentPage = _pages[^1];
                },
                token);

            CleanupPageAfterInitialization(removed);
            return true;
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
            if (_pages.Count <= 1 || _isDisposed)
            {
                return false;
            }

            List<PageViewModel> removed = [];
            await RunOnUiThreadAsync(
                () =>
                {
                    removed.AddRange(_pages.Skip(1));
                    _pages.RemoveRange(1, _pages.Count - 1);
                    CurrentPage = _pages[0];
                },
                token);

            foreach (var page in removed)
            {
                CleanupPageAfterInitialization(page);
            }

            return true;
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    private async Task<bool> InitializePageAsync(PageViewModel page, CancellationToken cancellationToken)
    {
        if (page.IsInitialized || page.InitializeCommand is null)
        {
            return true;
        }

        try
        {
            await Task.Run(
                async () =>
                {
                    page.InitializeCommand.Execute(null);
                    if (page.InitializeCommand.ExecutionTask is Task executionTask)
                    {
                        await executionTask;
                    }
                },
                cancellationToken);
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

    private Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            action,
            cancellationToken,
            TaskCreationOptions.None,
            _scheduler);
    }

    private static void CleanupPage(PageViewModel? page)
    {
        if (page is null)
        {
            return;
        }

        try
        {
            page.SafeCleanup();
            if (page is IDisposable disposable)
            {
                disposable.Dispose();
            }
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
        _lifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
