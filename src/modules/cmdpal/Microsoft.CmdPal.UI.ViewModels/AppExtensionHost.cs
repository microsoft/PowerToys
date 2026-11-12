// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class AppExtensionHost : IExtensionHost
{
    private static readonly GlobalLogPageContext _globalLogPageContext = new();

    private static ulong _hostingHwnd;

    public ulong HostingHwnd => _hostingHwnd;

    public string LanguageOverride => string.Empty;

    public ObservableCollection<StatusMessageViewModel> StatusMessages { get; } = [];

    public static void SetHostHwnd(ulong hostHwnd) => _hostingHwnd = hostHwnd;

    public void DebugLog(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
#if DEBUG
        CoreLogger.LogDebug($"[Host message] {message}", memberName, sourceFilePath, sourceLineNumber);
#endif
    }

    public IAsyncAction HideStatus(IStatusMessage? message)
    {
        if (message is null)
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
        if (message is null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        switch (message.State)
        {
            case MessageState.Error:
                CoreLogger.LogError(message.Message);
                break;
            case MessageState.Warning:
                CoreLogger.LogWarning(message.Message);
                break;
            case MessageState.Info:
            default:
                CoreLogger.LogInfo(message.Message);
                break;
        }

        return Task.CompletedTask.AsAsyncAction();
    }

    public void ProcessHideStatusMessage(IStatusMessage message)
    {
        Task.Factory.StartNew(
            () =>
            {
                try
                {
                    var vm = StatusMessages.Where(messageVM => messageVM.Model.Unsafe == message).FirstOrDefault();
                    if (vm is not null)
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

    public void ProcessStatusMessage(IStatusMessage message, StatusContext context)
    {
        // If this message is already in the list of messages, just bring it to the top
        var oldVm = StatusMessages.Where(messageVM => messageVM.Model.Unsafe == message).FirstOrDefault();
        if (oldVm is not null)
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

        Task.Factory.StartNew(
            () =>
            {
                StatusMessages.Add(vm);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _globalLogPageContext.Scheduler);
    }

    public IAsyncAction ShowStatus(IStatusMessage? message, StatusContext context)
    {
        if (message is null)
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

    public abstract string? GetExtensionDisplayName();
}

public interface IAppHostService
{
    AppExtensionHost GetDefaultHost();

    AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost);

    ICommandProviderContext GetProviderContextForCommand(object? command, ICommandProviderContext? currentContext);
}
