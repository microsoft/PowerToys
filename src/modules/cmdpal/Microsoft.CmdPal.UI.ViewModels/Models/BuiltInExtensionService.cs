// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

public partial class BuiltInExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    private readonly ILogger _logger;
    private readonly TaskScheduler _taskScheduler;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsService _settingsService;

    private readonly IEnumerable<ICommandProvider> _commandProviders;
    private readonly Lock _commandProvidersLock = new();

    private readonly List<CommandProviderWrapper> _builtInCommandWrappers = [];
    private readonly SemaphoreSlim _getBuiltInCommandWrappersLock = new(1, 1);

    private readonly List<CommandProviderWrapper> _enabledBuiltInCommandWrappers = [];
    private readonly SemaphoreSlim _getEnabledBuiltInCommandWrappersLock = new(1, 1);

    private readonly List<TopLevelViewModel> _topLevelCommands = [];
    private readonly SemaphoreSlim _getTopLevelCommandsLock = new(1, 1);

    private WeakReference<IPageContext>? _weakPageContext;

    private bool isLoaded;

    public BuiltInExtensionService(
        IEnumerable<ICommandProvider> commandProviders,
        TaskScheduler taskScheduler,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        SettingsService settingsService,
        ILogger logger)
    {
        _logger = logger;
        _taskScheduler = taskScheduler;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _settingsService = settingsService;
        _commandProviders = commandProviders;
    }

    public async Task<IEnumerable<CommandProviderWrapper>> GetCommandProviderWrappersAsync(WeakReference<IPageContext> weakPageContext, bool includeDisabledExtensions = false)
    {
        _weakPageContext = weakPageContext;

        if (!isLoaded)
        {
            await LoadBuiltInsAsync();
        }

        if (includeDisabledExtensions)
        {
            await _getBuiltInCommandWrappersLock.WaitAsync();
            try
            {
                return _builtInCommandWrappers;
            }
            finally
            {
                _getBuiltInCommandWrappersLock.Release();
            }
        }
        else
        {
            await _getEnabledBuiltInCommandWrappersLock.WaitAsync();
            try
            {
                return _enabledBuiltInCommandWrappers;
            }
            finally
            {
                _getEnabledBuiltInCommandWrappersLock.Release();
            }
        }
    }

    public async Task<IEnumerable<TopLevelViewModel>> GetTopLevelCommandsAsync()
    {
        await _getTopLevelCommandsLock.WaitAsync();
        try
        {
            return _topLevelCommands;
        }
        finally
        {
            _getTopLevelCommandsLock.Release();
        }
    }

    public async Task SignalStopExtensionsAsync()
    {
        // We're buil-in. There's no stopping us.
    }

    public async Task EnableProviderAsync(string providerId)
    {
        await _getEnabledBuiltInCommandWrappersLock.WaitAsync();
        try
        {
            if (_enabledBuiltInCommandWrappers.Any(wrapper => wrapper.Id.Equals(providerId, StringComparison.Ordinal)))
            {
                return;
            }
        }
        finally
        {
            _getEnabledBuiltInCommandWrappersLock.Release();
        }

        await _getBuiltInCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = _builtInCommandWrappers.FirstOrDefault(wrapper => wrapper.Id.Equals(providerId, StringComparison.Ordinal));

            if (wrapper != null)
            {
                await _getEnabledBuiltInCommandWrappersLock.WaitAsync();
                try
                {
                    _enabledBuiltInCommandWrappers.Add(wrapper);
                }
                finally
                {
                    _getEnabledBuiltInCommandWrappersLock.Release();
                }

                var commands = await LoadTopLevelCommandsFromProvider(wrapper);
                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    foreach (var c in commands)
                    {
                        _topLevelCommands.Add(c);
                    }
                }
                finally
                {
                    _getTopLevelCommandsLock.Release();
                }

                OnCommandsAdded?.Invoke(wrapper, commands);
            }
        }
        finally
        {
            _getBuiltInCommandWrappersLock.Release();
        }
    }

    public async Task DisableProviderAsync(string providerId)
    {
        await _getEnabledBuiltInCommandWrappersLock.WaitAsync();
        try
        {
            var wrapper = _enabledBuiltInCommandWrappers.FirstOrDefault(wrapper => wrapper.Id.Equals(providerId, StringComparison.Ordinal));

            if (wrapper != null)
            {
                _enabledBuiltInCommandWrappers.Remove(wrapper);

                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    var commands = _topLevelCommands.Where(command => command.CommandProviderId.Equals(wrapper.Id, StringComparison.Ordinal));
                    foreach (var c in commands)
                    {
                        _topLevelCommands.Remove(c);
                    }

                    OnCommandsRemoved?.Invoke(wrapper, commands);
                }
                finally
                {
                    _getTopLevelCommandsLock.Release();
                }
            }
        }
        finally
        {
            _getEnabledBuiltInCommandWrappersLock.Release();
        }
    }

    private async Task LoadBuiltInsAsync()
    {
        var s = new Stopwatch();
        s.Start();

        var builtInProviders = _commandProviders;
        foreach (var provider in builtInProviders)
        {
            CommandProviderWrapper wrapper = new(provider, _taskScheduler, _hotkeyManager, _aliasManager, _logger);
            lock (_getBuiltInCommandWrappersLock)
            {
                _builtInCommandWrappers.Add(wrapper);
            }

            lock (_getEnabledBuiltInCommandWrappersLock)
            {
                _enabledBuiltInCommandWrappers.Add(wrapper);
            }

            var commands = await LoadTopLevelCommandsFromProvider(wrapper);
            lock (_getTopLevelCommandsLock)
            {
                foreach (var c in commands)
                {
                    _topLevelCommands.Add(c);
                }
            }
        }

        s.Stop();

        isLoaded = true;

        Log_LoadingBuiltInsTook(s.ElapsedMilliseconds);
    }

    private async Task<IEnumerable<TopLevelViewModel>> LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        await commandProvider.LoadTopLevelCommands(_settingsService, _weakPageContext!);

        var commands = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> commands = [];
                foreach (var item in commandProvider.TopLevelItems)
                {
                    commands.Add(item);
                }

                foreach (var item in commandProvider.FallbackItems)
                {
                    if (item.IsEnabled)
                    {
                        commands.Add(item);
                    }
                }

                return commands;
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _taskScheduler);

        commandProvider.CommandsChanged -= CommandProvider_CommandsChanged;
        commandProvider.CommandsChanged += CommandProvider_CommandsChanged;

        return commands;
    }

    private void CommandProvider_CommandsChanged(CommandProviderWrapper commandProviderWrapper, IItemsChangedEventArgs args)
    {
    }

    public void Dispose()
    {
        _getBuiltInCommandWrappersLock.Dispose();
        _getEnabledBuiltInCommandWrappersLock.Dispose();
        _getTopLevelCommandsLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Test()
    {
        OnCommandProviderAdded?.Invoke(null!, null!);
        OnCommandProviderRemoved?.Invoke(null!, null!);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading built-ins took {elapsedMs}ms")]
    partial void Log_LoadingBuiltInsTook(long elapsedMs);
}
