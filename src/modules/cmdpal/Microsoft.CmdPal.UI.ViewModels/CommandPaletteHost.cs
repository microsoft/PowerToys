// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandPaletteHost : IExtensionHost
{
    // Static singleton, so that we can access this from anywhere
    // Post MVVM - this should probably be like, a dependency injection thing.
    public static CommandPaletteHost Instance { get; } = new();

    private static readonly GlobalLogPageContext _globalLogPageContext = new();

    private static ulong _hostingHwnd;

    public ulong HostingHwnd => _hostingHwnd;

    public string LanguageOverride => string.Empty;

    public static ObservableCollection<LogMessageViewModel> LogMessages { get; } = [];

    public ObservableCollection<StatusMessageViewModel> StatusMessages { get; } = [];

    public IExtensionWrapper? Extension { get; }

    private readonly ICommandProvider? _builtInProvider;

    private CommandPaletteHost()
    {
    }

    public CommandPaletteHost(IExtensionWrapper source)
    {
        Extension = source;
    }

    public CommandPaletteHost(ICommandProvider builtInProvider)
    {
        _builtInProvider = builtInProvider;
    }

    public IAsyncAction ShowStatus(IStatusMessage? message, StatusContext context)
    {
        if (message == null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        Debug.WriteLine(message.Message);

        _ = Task.Run(() =>
        {
            ProcessStatusMessage(message, context);
        });

        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction HideStatus(IStatusMessage? message)
    {
        if (message == null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        _ = Task.Run(() =>
        {
            ProcessHideStatusMessage(message);
        });
        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction LogMessage(ILogMessage? message)
    {
        if (message == null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        Logger.LogDebug(message.Message);

        _ = Task.Run(() =>
        {
            ProcessLogMessage(message);
        });

        // We can't just make a LogMessageViewModel : ExtensionObjectViewModel
        // because we don't necessarily know the page context. Butts.
        return Task.CompletedTask.AsAsyncAction();
    }

    public void ProcessLogMessage(ILogMessage message)
    {
        var vm = new LogMessageViewModel(message, _globalLogPageContext);
        vm.SafeInitializePropertiesSynchronous();

        if (Extension != null)
        {
            vm.ExtensionPfn = Extension.PackageFamilyName;
        }

        Task.Factory.StartNew(
            () =>
            {
                LogMessages.Add(vm);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _globalLogPageContext.Scheduler);
    }

    public void ProcessStatusMessage(IStatusMessage message, StatusContext context)
    {
        // If this message is already in the list of messages, just bring it to the top
        var oldVm = StatusMessages.Where(messageVM => messageVM.Model.Unsafe == message).FirstOrDefault();
        if (oldVm != null)
        {
            Task.Factory.StartNew(
                () =>
                {
                    StatusMessages.Remove(oldVm);
                    StatusMessages.Add(oldVm);
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                _globalLogPageContext.Scheduler);
            return;
        }

        var vm = new StatusMessageViewModel(message, new(_globalLogPageContext));
        vm.SafeInitializePropertiesSynchronous();

        if (Extension != null)
        {
            vm.ExtensionPfn = Extension.PackageFamilyName;
        }

        Task.Factory.StartNew(
            () =>
            {
                StatusMessages.Add(vm);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _globalLogPageContext.Scheduler);
    }

    public void ProcessHideStatusMessage(IStatusMessage message)
    {
        Task.Factory.StartNew(
            () =>
            {
                try
                {
                    var vm = StatusMessages.Where(messageVM => messageVM.Model.Unsafe == message).FirstOrDefault();
                    if (vm != null)
                    {
                        StatusMessages.Remove(vm);
                    }
                }
                catch
                {
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _globalLogPageContext.Scheduler);
    }

    public static void SetHostHwnd(ulong hostHwnd) => _hostingHwnd = hostHwnd;

    public void DebugLog(string message)
    {
#if DEBUG
        this.ProcessLogMessage(new LogMessage(message));
#endif
    }

    public void Log(string message)
    {
        this.ProcessLogMessage(new LogMessage(message));
    }
}
