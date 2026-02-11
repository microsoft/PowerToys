// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public partial class BuiltInExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

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

    public async Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
    {
        _weakPageContext = weakPageContext;

        if (!isLoaded)
        {
            await LoadBuiltInsAsync();
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
            foreach (var wrapper in _enabledBuiltInCommandWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
        finally
        {
            _getEnabledBuiltInCommandWrappersLock.Release();
        }

        await _getBuiltInCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = null;

            foreach (var builtInWrapper in _builtInCommandWrappers)
            {
                if (builtInWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = builtInWrapper;
                }
            }

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
            CommandProviderWrapper? wrapper = null;

            foreach (var enabledWrapper in _enabledBuiltInCommandWrappers)
            {
                if (enabledWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = enabledWrapper;
                }
            }

            if (wrapper != null)
            {
                _enabledBuiltInCommandWrappers.Remove(wrapper);

                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    List<TopLevelViewModel> commands = [];

                    foreach (var topLevelCommand in _topLevelCommands)
                    {
                        if (topLevelCommand.CommandProviderId.Equals(wrapper.Id, StringComparison.Ordinal))
                        {
                            commands.Add(topLevelCommand);
                        }
                    }

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

            var providerSettings = _settingsService.CurrentSettings.GetProviderSettings(wrapper);

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

            OnCommandProviderAdded?.Invoke(this, new[] { wrapper });
            OnCommandsAdded?.Invoke(wrapper, commands);
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

    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args) =>
       _ = Task.Run(async () => await UpdateCommandsForProvider(sender, args));

    /// <summary>
    /// Called when a command provider raises its ItemsChanged event. We'll
    /// remove the old commands from the top-level list and try to put the new
    /// ones in the same place in the list.
    /// </summary>
    private async Task UpdateCommandsForProvider(CommandProviderWrapper sender, IItemsChangedEventArgs args)
    {
        var topLevelItems = await LoadTopLevelCommandsFromProvider(sender);

        List<TopLevelViewModel> newTopLevelItems = [.. topLevelItems];
        foreach (var i in sender.FallbackItems)
        {
            if (i.IsEnabled)
            {
                newTopLevelItems.Add(i);
            }
        }

        // Modify the TopLevelCommands under shared lock; event if we clone it, we don't want
        // TopLevelCommands to get modified while we're working on it. Otherwise, we might
        // out clone would be stale at the end of this method.
        await _getTopLevelCommandsLock.WaitAsync();
        try
        {
            // Work on a clone of the list, so that we can just do one atomic
            // update to the actual observable list at the end
            // TODO: just added a lock around all of this anyway, but keeping the clone
            // while looking on some other ways to improve this; can be removed later
            // .
            // The clone will be everything except the commands
            // from the provider that raised the event
            List<TopLevelViewModel> clone = [.. _topLevelCommands];
            clone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            clone.AddRange(newTopLevelItems);

            ListHelpers.InPlaceUpdateList(_topLevelCommands, clone);
        }
        finally
        {
            _getTopLevelCommandsLock.Release();
        }

        return;
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
