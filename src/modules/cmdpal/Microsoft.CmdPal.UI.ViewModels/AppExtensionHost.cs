// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Auth;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class AppExtensionHost : IExtensionHost, IExtensionHost2
{
    private static readonly GlobalLogPageContext _globalLogPageContext = new();

    private static ulong _hostingHwnd;

    public static ObservableCollection<LogMessageViewModel> LogMessages { get; } = [];

    public ulong HostingHwnd => _hostingHwnd;

    public string LanguageOverride => string.Empty;

    public ObservableCollection<StatusMessageViewModel> StatusMessages { get; } = [];

    public static void SetHostHwnd(ulong hostHwnd) => _hostingHwnd = hostHwnd;

    public void DebugLog(string message)
    {
#if DEBUG
        this.ProcessLogMessage(new LogMessage(message));
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

    public void Log(string message)
    {
        this.ProcessLogMessage(new LogMessage(message));
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

        _ = Task.Run(() =>
        {
            ProcessLogMessage(message);
        });

        // We can't just make a LogMessageViewModel : ExtensionObjectViewModel
        // because we don't necessarily know the page context. Butts.
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

    public void ProcessLogMessage(ILogMessage message)
    {
        var vm = new LogMessageViewModel(message, _globalLogPageContext);
        vm.SafeInitializePropertiesSynchronous();

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

    /// <summary>
    /// <see cref="IExtensionHost2"/>. Delegate the interactive authorization flow
    /// to the process-wide <see cref="AuthBrokerService"/>, passing this host so
    /// it can surface a cancelable, non-blocking status while the browser is open.
    /// </summary>
    public IAsyncOperation<IAuthorizationResult> RequestAuthorizationAsync(IAuthorizationRequest request)
        => AuthBrokerService.Instance.RequestAuthorizationAsync(request, this);

    /// <summary>
    /// <see cref="IExtensionHost2"/>. Navigate Command Palette to a page owned by
    /// the extension. Reuses the existing navigation pipeline: a
    /// <see cref="PerformCommandMessage"/> with <see cref="PerformCommandMessage.ShowWindowIfPage"/>
    /// set foregrounds the window and navigates when the command is a page. When
    /// the mode is GoHome or GoBack, the stack is reset first via the matching
    /// message. A null page is treated as a graceful no-op.
    /// </summary>
    public IAsyncAction GoToPageAsync(ICommand? page, NavigationMode navigationMode)
    {
        if (page is null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        return Task.Run(() =>
        {
            switch (navigationMode)
            {
                case NavigationMode.GoHome:
                    WeakReferenceMessenger.Default.Send<GoHomeMessage>(new(WithAnimation: false, FocusSearch: false));
                    break;
                case NavigationMode.GoBack:
                    WeakReferenceMessenger.Default.Send<GoBackMessage>(new(WithAnimation: false, FocusSearch: false));
                    break;
                case NavigationMode.Push:
                default:
                    break;
            }

            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(
                new(new ExtensionObject<ICommand>(page)) { ShowWindowIfPage = true });
        }).AsAsyncAction();
    }

    public abstract string? GetExtensionDisplayName();
}

public interface IAppHostService
{
    AppExtensionHost GetDefaultHost();

    AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost);

    ICommandProviderContext GetProviderContextForCommand(object? command, ICommandProviderContext? currentContext);
}
