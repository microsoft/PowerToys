// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Extension service for built-in (in-proc) <see cref="ICommandProvider"/> instances.
/// Wraps each provider into a <see cref="CommandProviderWrapper"/>, loads their
/// top-level commands, and fires the <see cref="IExtensionService"/> events so that
/// <c>TopLevelCommandManager</c> can consume them identically to out-of-proc extensions.
/// </summary>
public sealed partial class BuiltInExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

#pragma warning disable CS0067 // Event is declared by IExtensionService; raised when stop logic is implemented.
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;
#pragma warning restore CS0067

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    private readonly IEnumerable<ICommandProvider> _builtInProviders;
    private readonly TaskScheduler _mainThread;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<BuiltInExtensionService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly List<CommandProviderWrapper> _allWrappers = [];
    private readonly SemaphoreSlim _allWrappersLock = new(1, 1);

    private readonly List<CommandProviderWrapper> _enabledWrappers = [];
    private readonly SemaphoreSlim _enabledWrappersLock = new(1, 1);

    private readonly List<TopLevelViewModel> _topLevelCommands = [];
    private readonly SemaphoreSlim _topLevelCommandsLock = new(1, 1);

    private WeakReference<IPageContext>? _weakPageContext;
    private bool _isLoaded;

    public BuiltInExtensionService(
        IEnumerable<ICommandProvider> builtInProviders,
        TaskScheduler mainThread,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        ISettingsService settingsService,
        ILoggerFactory loggerFactory)
    {
        _builtInProviders = builtInProviders;
        _mainThread = mainThread;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _settingsService = settingsService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BuiltInExtensionService>();
    }

    public async Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
    {
        _weakPageContext = weakPageContext;

        if (!_isLoaded)
        {
            await LoadBuiltInsAsync();
        }
    }

    public Task SignalStopExtensionsAsync()
    {
        // Built-in providers live in-proc — nothing to tear down.
        return Task.CompletedTask;
    }

    public async Task EnableProviderAsync(string providerId)
    {
        // If already enabled, bail out.
        await _enabledWrappersLock.WaitAsync();
        try
        {
            foreach (var wrapper in _enabledWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
        finally
        {
            _enabledWrappersLock.Release();
        }

        // Find the wrapper in the full list and enable it.
        await _allWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? target = null;

            foreach (var wrapper in _allWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    target = wrapper;
                    break;
                }
            }

            if (target is not null)
            {
                await _enabledWrappersLock.WaitAsync();
                try
                {
                    _enabledWrappers.Add(target);
                }
                finally
                {
                    _enabledWrappersLock.Release();
                }

                var commands = await LoadTopLevelCommandsFromProvider(target);

                await _topLevelCommandsLock.WaitAsync();
                try
                {
                    foreach (var c in commands)
                    {
                        _topLevelCommands.Add(c);
                    }
                }
                finally
                {
                    _topLevelCommandsLock.Release();
                }

                OnCommandsAdded?.Invoke(target, commands);
            }
        }
        finally
        {
            _allWrappersLock.Release();
        }
    }

    public async Task DisableProviderAsync(string providerId)
    {
        await _enabledWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? target = null;

            foreach (var wrapper in _enabledWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    target = wrapper;
                    break;
                }
            }

            if (target is null)
            {
                return;
            }

            _enabledWrappers.Remove(target);

            await _topLevelCommandsLock.WaitAsync();
            try
            {
                List<TopLevelViewModel> removed = [];

                foreach (var cmd in _topLevelCommands)
                {
                    if (cmd.CommandProviderId.Equals(target.Id, StringComparison.Ordinal))
                    {
                        removed.Add(cmd);
                    }
                }

                foreach (var cmd in removed)
                {
                    _topLevelCommands.Remove(cmd);
                }

                OnCommandsRemoved?.Invoke(target, removed);
            }
            finally
            {
                _topLevelCommandsLock.Release();
            }
        }
        finally
        {
            _enabledWrappersLock.Release();
        }
    }

    private async Task LoadBuiltInsAsync()
    {
        var sw = Stopwatch.StartNew();
        var wrapperLogger = _loggerFactory.CreateLogger<CommandProviderWrapper>();

        foreach (var provider in _builtInProviders)
        {
            var wrapper = new CommandProviderWrapper(
                provider,
                _mainThread,
                _hotkeyManager,
                _aliasManager,
                wrapperLogger);

            await _allWrappersLock.WaitAsync();
            try
            {
                _allWrappers.Add(wrapper);

                await _enabledWrappersLock.WaitAsync();
                try
                {
                    _enabledWrappers.Add(wrapper);
                }
                finally
                {
                    _enabledWrappersLock.Release();
                }
            }
            finally
            {
                _allWrappersLock.Release();
            }

            var commands = await LoadTopLevelCommandsFromProvider(wrapper);

            await _topLevelCommandsLock.WaitAsync();
            try
            {
                foreach (var c in commands)
                {
                    _topLevelCommands.Add(c);
                }
            }
            finally
            {
                _topLevelCommandsLock.Release();
            }

            OnCommandProviderAdded?.Invoke(this, new[] { wrapper });
            OnCommandsAdded?.Invoke(wrapper, commands);
        }

        sw.Stop();
        _isLoaded = true;

        LogLoadingBuiltInsTook(sw.ElapsedMilliseconds);
    }

    private async Task<IEnumerable<TopLevelViewModel>> LoadTopLevelCommandsFromProvider(CommandProviderWrapper wrapper)
    {
        await wrapper.LoadTopLevelCommands(_settingsService, _weakPageContext!);

        var commands = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> result = [];

                foreach (var item in wrapper.TopLevelItems)
                {
                    result.Add(item);
                }

                foreach (var item in wrapper.FallbackItems)
                {
                    if (item.IsEnabled)
                    {
                        result.Add(item);
                    }
                }

                return result;
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _mainThread);

        // Re-subscribe to catch future changes from this provider.
        wrapper.CommandsChanged -= CommandProvider_CommandsChanged;
        wrapper.CommandsChanged += CommandProvider_CommandsChanged;

        return commands;
    }

    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args) =>
        _ = Task.Run(async () => await UpdateCommandsForProvider(sender));

    /// <summary>
    /// Called when a built-in provider raises its <c>ItemsChanged</c> event.
    /// Reloads top-level commands and replaces the old entries in the local list.
    /// </summary>
    private async Task UpdateCommandsForProvider(CommandProviderWrapper sender)
    {
        var freshCommands = await LoadTopLevelCommandsFromProvider(sender);
        List<TopLevelViewModel> newItems = [.. freshCommands];

        await _topLevelCommandsLock.WaitAsync();
        try
        {
            List<TopLevelViewModel> clone = [.. _topLevelCommands];
            clone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            clone.AddRange(newItems);

            ListHelpers.InPlaceUpdateList(_topLevelCommands, clone);
        }
        finally
        {
            _topLevelCommandsLock.Release();
        }
    }

    public void Dispose()
    {
        _allWrappersLock.Dispose();
        _enabledWrappersLock.Dispose();
        _topLevelCommandsLock.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── LoggerMessage source-generated methods ──────────────────────────
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading built-ins took {ElapsedMs}ms")]
    partial void LogLoadingBuiltInsTook(long elapsedMs);
}
