// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CommunityToolkit.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using WinRT;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ShellViewModel(IServiceProvider _serviceProvider, TaskScheduler _scheduler) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLoaded { get; set; } = false;

    [ObservableProperty]
    public partial DetailsViewModel? Details { get; set; }

    [ObservableProperty]
    public partial bool IsDetailsVisible { get; set; }

    [ObservableProperty]
    public partial PageViewModel CurrentPage { get; set; } = new LoadingPageViewModel(null, _scheduler);

    private MainListPage? _mainListPage;

    private IExtensionWrapper? _activeExtension;

    [RelayCommand]
    public async Task<bool> LoadAsync()
    {
        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>();
        await tlcManager!.LoadBuiltinsAsync();
        IsLoaded = true;

        // Built-ins have loaded. We can display our page at this point.
        _mainListPage = new MainListPage(_serviceProvider);
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(new ExtensionObject<ICommand>(_mainListPage)));

        _ = Task.Run(async () =>
        {
            // After loading built-ins, and starting navigation, kick off a thread to load extensions.
            tlcManager.LoadExtensionsCommand.Execute(null);

            await tlcManager.LoadExtensionsCommand.ExecutionTask!;
            if (tlcManager.LoadExtensionsCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
            {
                // TODO: Handle failure case
            }
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
            && viewModel.InitializeCommand != null)
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
                            var result = (bool)viewModel.InitializeCommand.ExecutionTask.GetResultOrDefault()!;

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

    public void PerformTopLevelCommand(PerformCommandMessage message)
    {
        if (_mainListPage == null)
        {
            return;
        }

        if (message.Context is IListItem listItem)
        {
            _mainListPage.UpdateHistory(listItem);
        }
    }

    public void SetActiveExtension(IExtensionWrapper? extension)
    {
        if (extension != _activeExtension)
        {
            // There's not really a CoDisallowSetForegroundWindow, so we don't
            // need to handle that
            _activeExtension = extension;

            var extensionWinRtObject = _activeExtension?.GetExtensionObject();
            if (extensionWinRtObject != null)
            {
                try
                {
                    unsafe
                    {
                        var winrtObj = (IWinRTObject)extensionWinRtObject;
                        var intPtr = winrtObj.NativeObject.ThisPtr;
                        var hr = Native.CoAllowSetForegroundWindow(intPtr);
                        if (hr != 0)
                        {
                            Logger.LogWarning($"Error giving foreground rights: 0x{hr.Value:X8}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }
        }
    }

    public void GoHome()
    {
        SetActiveExtension(null);
    }

    // You may ask yourself, why aren't we using CsWin32 for this?
    // The CsWin32 projected version includes some object marshalling, like so:
    //
    // HRESULT CoAllowSetForegroundWindow([MarshalAs(UnmanagedType.IUnknown)] object pUnk,...)
    //
    // And if you do it like that, then the IForegroundTransfer interface isn't marshalled correctly
    internal sealed class Native
    {
        [DllImport("OLE32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows5.0")]
        internal static extern unsafe global::Windows.Win32.Foundation.HRESULT CoAllowSetForegroundWindow(nint pUnk, [Optional] void* lpvReserved);
    }
}
