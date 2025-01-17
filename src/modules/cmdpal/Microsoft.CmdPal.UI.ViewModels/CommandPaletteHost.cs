// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandPaletteHost : IExtensionHost
{
    // Static singleton, so that we can access this from anywhere
    // Post MVVM - this should probably be like, a dependency injection thing.
    public static CommandPaletteHost Instance { get; } = new();

    private static readonly GlobalLogPageContext _globalLogPageContext = new();

    private ulong _hostHwnd;

    public ulong HostingHwnd => _hostHwnd;

    public string LanguageOverride => string.Empty;

    public static ObservableCollection<LogMessageViewModel> LogMessages { get; } = new();

    public ObservableCollection<StatusMessageViewModel> StatusMessages { get; } = new();

    private readonly IExtensionWrapper? _source;

    public IExtensionWrapper? Extension => _source;

    private CommandPaletteHost()
    {
    }

    public CommandPaletteHost(IExtensionWrapper source)
    {
        _source = source;
    }

    public IAsyncAction ShowStatus(IStatusMessage message)
    {
        Debug.WriteLine(message.Message);

        _ = Task.Run(() =>
        {
            ProcessStatusMessage(message);
        });

        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction HideStatus(IStatusMessage message)
    {
        return Task.CompletedTask.AsAsyncAction();
    }

    public IAsyncAction LogMessage(ILogMessage message)
    {
        Debug.WriteLine(message.Message);

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

        if (_source != null)
        {
            vm.ExtensionPfn = _source.PackageFamilyName;
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

    public void ProcessStatusMessage(IStatusMessage message)
    {
        var vm = new StatusMessageViewModel(message, _globalLogPageContext);
        vm.SafeInitializePropertiesSynchronous();

        if (_source != null)
        {
            vm.ExtensionPfn = _source.PackageFamilyName;
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

    public void SetHostHwnd(ulong hostHwnd)
    {
        _hostHwnd = hostHwnd;
    }
}
