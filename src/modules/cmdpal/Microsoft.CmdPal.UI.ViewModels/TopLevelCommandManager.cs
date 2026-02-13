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
    private readonly ICommandProviderCache _commandProviderCache;
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
        ICommandProviderCache commandProviderCache,
        ILogger logger)
    {
        this._logger = logger;
        _extensionServices = extensionServices;
        _taskScheduler = taskScheduler;
        _settingsService = settingsService;
        _commandProviderCache = commandProviderCache;
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
        var extensionService = _serviceProvider.GetService<IExtensionService>()!;
        await extensionService.SignalStopExtensionsAsync();

        lock (TopLevelCommands)
        {
            TopLevelCommands.Clear();
        }

        await LoadBuiltinsAsync();
        _ = Task.Run(LoadExtensionsAsync);
    }

    // Load commands from our extensions. Called on a background thread.
    // Currently, this
    // * queries the package catalog,
    // * starts all the extensions,
    // * then fetches the top-level commands from them.
    // TODO In the future, we'll probably abstract some of this away, to have
    // separate extension tracking vs stub loading.
    [RelayCommand]
    public async Task<bool> LoadExtensionsAsync()
    {
        var extensionService = _serviceProvider.GetService<IExtensionService>()!;

        extensionService.OnExtensionAdded -= ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved -= ExtensionService_OnExtensionRemoved;

        var extensions = (await extensionService.GetInstalledExtensionsAsync()).ToImmutableList();
        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.Clear();
        }

        if (extensions is not null)
        {
            await StartExtensionsAndGetCommands(extensions);
        }

        extensionService.OnExtensionAdded += ExtensionService_OnExtensionAdded;
        extensionService.OnExtensionRemoved += ExtensionService_OnExtensionRemoved;

        IsLoading = false;

        // Send on the current thread; receivers should marshal to UI if needed
        WeakReferenceMessenger.Default.Send<ReloadFinishedMessage>();

        return true;
    }

    private void ExtensionService_OnExtensionAdded(IExtensionService sender, IEnumerable<IExtensionWrapper> extensions)
    {
        // When we get an extension install event, hop off to a BG thread
        _ = Task.Run(async () =>
        {
            // for each newly installed extension, start it and get commands
            // from it. One single package might have more than one
            // IExtensionWrapper in it.
            await StartExtensionsAndGetCommands(extensions);
        });
    }

    private async Task StartExtensionsAndGetCommands(IEnumerable<IExtensionWrapper> extensions)
    {
        var timer = new Stopwatch();
        timer.Start();

        // Start all extensions in parallel
        var startTasks = extensions.Select(StartExtensionWithTimeoutAsync);

        // Wait for all extensions to start
        var wrappers = (await Task.WhenAll(startTasks)).Where(wrapper => wrapper is not null).Select(w => w!).ToList();

        lock (_commandProvidersLock)
        {
            _extensionCommandProviders.AddRange(wrappers);
        }

        // Load the commands from the providers in parallel
        var loadTasks = wrappers.Select(LoadCommandsWithTimeoutAsync);

        var commandSets = (await Task.WhenAll(loadTasks)).Where(results => results is not null).Select(r => r!).ToList();

        lock (TopLevelCommands)
        {
            foreach (var commands in commandSets)
            {
                foreach (var c in commands)
                {
                    TopLevelCommands.Add(c);
                }
            }
        }

        timer.Stop();
        Logger.LogDebug($"Loading extensions took {timer.ElapsedMilliseconds} ms");
    }

    private async Task<CommandProviderWrapper?> StartExtensionWithTimeoutAsync(IExtensionWrapper extension)
    {
        Logger.LogDebug($"Starting {extension.PackageFullName}");
        try
        {
            await extension.StartExtensionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return new CommandProviderWrapper(extension, _taskScheduler, _commandProviderCache);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start extension {extension.PackageFullName}: {ex}");
            return null; // Return null for failed extensions
        }
    }

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
