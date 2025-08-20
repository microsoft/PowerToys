// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ShellViewModel : ObservableObject,
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>
{
    private readonly IRootPageService _rootPageService;
    private readonly IAppHostService _appHostService;
    private readonly TaskScheduler _scheduler;
    private readonly IPageViewModelFactoryService _pageViewModelFactory;
    private readonly Lock _invokeLock = new();
    private Task? _handleInvokeTask;

    [ObservableProperty]
    public partial bool IsLoaded { get; set; } = false;

    [ObservableProperty]
    public partial DetailsViewModel? Details { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsVisible { get; set; }

    private PageViewModel _currentPage;

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            var oldValue = _currentPage;
            if (SetProperty(ref _currentPage, value))
            {
                if (oldValue is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }
            }
        }
    }

    private IPage? _rootPage;

    private bool _isNested;

    public bool IsNested { get => _isNested; }

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

        _currentPage = new LoadingPageViewModel(null, _scheduler, appHostService.GetDefaultHost());

        // Register to receive messages
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);
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

    public void LoadPageViewModel(PageViewModel viewModel)
    {
        // Note: We removed the general loading state, extensions sometimes use their `IsLoading`, but it's inconsistently implemented it seems.
        // IsInitialized is our main indicator of the general overall state of loading props/items from a page we use for the progress bar
        // This triggers that load generally with the InitializeCommand asynchronously when we navigate to a page.
        // We could re-track the page loading status, if we need it more granularly below again, but between the initialized and error message, we can infer some status.
        // We could also maybe move this thread offloading we do for loading into PageViewModel and better communicate between the two... a few different options.

        ////LoadedState = ViewModelLoadedState.Loading;
        if (!viewModel.IsInitialized
            && viewModel.InitializeCommand is not null)
        {
            _ = Task.Run(async () =>
            {
                // You know, this creates the situation where we wait for
                // both loading page properties, AND the items, before we
                // display anything.
                //
                // We almost need to do an async await on initialize, then
                // just a fire-and-forget on FetchItems.
                // RE: We do set the CurrentPage in ShellPage.xaml.cs as well, so, we kind of are doing two different things here.
                // Definitely some more clean-up to do, but at least its centralized to one spot now.
                viewModel.InitializeCommand.Execute(null);

                await viewModel.InitializeCommand.ExecutionTask!;

                if (viewModel.InitializeCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
                {
                    // TODO: Handle failure case
                    if (viewModel.InitializeCommand.ExecutionTask.Exception is AggregateException ex)
                    {
                        Logger.LogError(ex.ToString());
                    }

                    // TODO GH #239 switch back when using the new MD text block
                    // _ = _queue.EnqueueAsync(() =>
                    /*_queue.TryEnqueue(new(() =>
                    {
                        LoadedState = ViewModelLoadedState.Error;
                    }));*/
                }
                else
                {
                    // TODO GH #239 switch back when using the new MD text block
                    // _ = _queue.EnqueueAsync(() =>
                    _ = Task.Factory.StartNew(
                        () =>
                        {
                            // bool f = await viewModel.InitializeCommand.ExecutionTask.;
                            // var result = viewModel.InitializeCommand.ExecutionTask.GetResultOrDefault()!;
                            // var result = viewModel.InitializeCommand.ExecutionTask.GetResultOrDefault<bool?>()!;
                            CurrentPage = viewModel; // result ? viewModel : null;
                            ////LoadedState = result ? ViewModelLoadedState.Loaded : ViewModelLoadedState.Error;
                        },
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        _scheduler);
                }
            });
        }
        else
        {
            CurrentPage = viewModel;
            ////LoadedState = ViewModelLoadedState.Loaded;
        }
    }

    public void Receive(PerformCommandMessage message)
    {
        PerformCommand(message);
    }

    private void PerformCommand(PerformCommandMessage message)
    {
        var command = message.Command.Unsafe;
        if (command is null)
        {
            return;
        }

        var host = _appHostService.GetHostForCommand(message.Context, CurrentPage.ExtensionHost);

        _rootPageService.OnPerformCommand(message.Context, !CurrentPage.IsNested, host);

        try
        {
            if (command is IPage page)
            {
                Logger.LogDebug($"Navigating to page");

                var isMainPage = command == _rootPage;
                _isNested = !isMainPage;

                // Construct our ViewModel of the appropriate type and pass it the UI Thread context.
                var pageViewModel = _pageViewModelFactory.TryCreatePageViewModel(page, _isNested, host);
                if (pageViewModel is null)
                {
                    Logger.LogError($"Failed to create ViewModel for page {page.GetType().Name}");
                    throw new NotSupportedException();
                }

                // Kick off async loading of our ViewModel
                LoadPageViewModel(pageViewModel);
                OnUIThread(() => { WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(null)); });
                WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(pageViewModel, message.WithAnimation));

                // Note: Originally we set our page back in the ViewModel here, but that now happens in response to the Frame navigating triggered from the above
                // See RootFrame_Navigated event handler.
            }
            else if (command is IInvokableCommand invokable)
            {
                Logger.LogDebug($"Invoking command");

                WeakReferenceMessenger.Default.Send<BeginInvokeMessage>();
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
        try
        {
            // Call out to extension process.
            // * May fail!
            // * May never return!
            var result = invokable.Invoke(message.Context);

            // But if it did succeed, we need to handle the result.
            UnsafeHandleCommandResult(result);

            _handleInvokeTask = null;
        }
        catch (Exception ex)
        {
            _handleInvokeTask = null;

            // TODO: It would be better to do this as a page exception, rather
            // than a silent log message.
            host?.Log(ex.Message);
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
        Logger.LogDebug($"handling {kind.ToString()}");

        WeakReferenceMessenger.Default.Send<CmdPalInvokeResultMessage>(new(kind));
        switch (kind)
        {
            case CommandResultKind.Dismiss:
                {
                    // Reset the palette to the main page and dismiss
                    GoHome(withAnimation: false, focusSearch: false);
                    WeakReferenceMessenger.Default.Send<DismissMessage>();
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
                    WeakReferenceMessenger.Default.Send<DismissMessage>();
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

    private void OnUIThread(Action action)
    {
        _ = Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.None,
            _scheduler);
    }
}
