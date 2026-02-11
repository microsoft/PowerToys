// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class TopLevelCommandManager : ObservableObject,
    IRecipient<ReloadCommandsMessage>,
    IPageContext,
    IDisposable
{
    private readonly TaskScheduler _taskScheduler;
    private readonly IEnumerable<IExtensionService> _extensionServices;
    private readonly ILogger _logger;
    private readonly SettingsService _settingsService;

    private readonly List<CommandProviderWrapper> _commandProviderWrappers = [];
    private readonly Lock _commandProviderWrappersLock = new();

    private readonly SupersedingAsyncGate _reloadCommandsGate;

    TaskScheduler IPageContext.Scheduler => _taskScheduler;

    public TopLevelCommandManager(
        IEnumerable<IExtensionService> extensionServices,
        TaskScheduler taskScheduler,
        SettingsService settingsService,
        ILogger logger)
    {
        this._logger = logger;
        _extensionServices = extensionServices;
        _taskScheduler = taskScheduler;
        _settingsService = settingsService;
        WeakReferenceMessenger.Default.Register<ReloadCommandsMessage>(this);
        _reloadCommandsGate = new(ReloadAllCommandsAsyncCore);
    }

    public ObservableCollection<TopLevelViewModel> TopLevelCommands { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    public IEnumerable<CommandProviderWrapper> CommandProviders
    {
        get
        {
            lock (_commandProviderWrappers)
            {
                return _commandProviderWrappers.ToList();
            }
        }
    }

    [RelayCommand]
    public async Task LoadExtensionsAsync()
    {
        lock (_commandProviderWrappers)
        {
            _commandProviderWrappers.Clear();
        }

        var weakSelf = new WeakReference<IPageContext>(this);

        foreach (var extensionService in _extensionServices)
        {
            extensionService.OnCommandProviderAdded -= ExtensionService_OnProviderAdded;
            extensionService.OnCommandProviderRemoved -= ExtensionService_OnProviderRemoved;
            extensionService.OnCommandsAdded -= ExtensionService_OnCommandsAdded;
            extensionService.OnCommandsRemoved -= ExtensionService_OnCommandsRemoved;

            _ = Task.Run(async () =>
            {
                await extensionService.SignalStartExtensionsAsync(weakSelf);
            });

            extensionService.OnCommandProviderAdded += ExtensionService_OnProviderAdded;
            extensionService.OnCommandProviderRemoved += ExtensionService_OnProviderRemoved;
            extensionService.OnCommandsAdded += ExtensionService_OnCommandsAdded;
            extensionService.OnCommandsRemoved += ExtensionService_OnCommandsRemoved;
        }

        IsLoading = false;

        // Send on the current thread; receivers should marshal to UI if needed
        WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();
    }

    private void ExtensionService_OnProviderRemoved(IExtensionService sender, IEnumerable<CommandProviderWrapper> args)
    {
    }

    private void ExtensionService_OnProviderAdded(IExtensionService sender, IEnumerable<CommandProviderWrapper> args)
    {
    }

    private void ExtensionService_OnCommandsAdded(CommandProviderWrapper commandProviderWrapper, IEnumerable<TopLevelViewModel> commands)
    {
        _ = Task.Run(async () =>
        {
            await Task.Factory.StartNew(
             () =>
             {
                 lock (TopLevelCommands)
                 {
                     foreach (var command in commands)
                     {
                         TopLevelCommands.Add(command);
                     }
                 }
             },
             CancellationToken.None,
             TaskCreationOptions.None,
             _taskScheduler);
        });
    }

    private void ExtensionService_OnCommandsRemoved(CommandProviderWrapper commandProviderWrapper, IEnumerable<TopLevelViewModel> commands)
    {
        _ = Task.Run(async () =>
        {
            await Task.Factory.StartNew(
             () =>
             {
                 lock (TopLevelCommands)
                 {
                     foreach (var command in commands)
                     {
                         TopLevelCommands.Remove(command);
                     }
                 }
             },
             CancellationToken.None,
             TaskCreationOptions.None,
             _taskScheduler);
        });
    }

    public async Task ReloadAllCommandsAsync()
    {
        // gate ensures that the reload is serialized and if multiple calls
        // request a reload, only the first and the last one will be executed.
        // this should be superseded with a cancellable version.
        await _reloadCommandsGate.ExecuteAsync(CancellationToken.None);
    }

    private async Task ReloadAllCommandsAsyncCore(CancellationToken cancellationToken)
    {
        IsLoading = true;

        foreach (var extensionService in _extensionServices)
        {
            await extensionService.SignalStopExtensionsAsync();
        }

        lock (TopLevelCommands)
        {
            TopLevelCommands.Clear();
        }

        _ = Task.Run(LoadExtensionsAsync, cancellationToken);
    }

    public TopLevelViewModel? LookupCommand(string id)
    {
        lock (TopLevelCommands)
        {
            foreach (var command in TopLevelCommands)
            {
                if (command.Id == id)
                {
                    return command;
                }
            }
        }

        return null;
    }

    public void Receive(ReloadCommandsMessage message) =>
        ReloadAllCommandsAsync().ConfigureAwait(false);

    void IPageContext.ShowException(Exception ex, string? extensionHint)
    {
        var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint ?? "TopLevelCommandManager");
        Log_ExceptionOccurred(message);
    }

    internal bool IsProviderActive(string id)
    {
        lock (_commandProviderWrappers)
        {
            return _commandProviderWrappers.Any(wrapper => wrapper.Id == id && wrapper.IsActive);
        }
    }

    public void Dispose()
    {
        _reloadCommandsGate.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading built-ins took {elapsedMs}ms")]
    partial void Log_LoadingBuiltInsTook(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading extensions took {elapsedMs} ms")]
    partial void Log_LoadingExtensionsTook(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting {packageFullName}")]
    partial void Log_StartingExtension(string? packageFullName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start extension {packageFullName}")]
    partial void Log_FailedToStartExtension(string? packageFullName, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Loading commands from {packageFullName} timed out")]
    partial void Log_LoadingCommandsTimedOut(string? packageFullName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load commands for extension {packageFullName}")]
    partial void Log_FailedToLoadCommands(string? packageFullName, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "{message}")]
    partial void Log_ExceptionOccurred(string message);
}
