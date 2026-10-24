// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Persistence;

/// <summary>Loads and persists immutable command-alias maps independently of Apps preferences.</summary>
internal sealed partial class AppCommandAliasStore : IDisposable
{
    private readonly Lock _stateLock = new();
    private readonly MEL.ILogger<AppCommandAliasStore> _logger;
    private FrozenDictionary<string, string> _appCommandAliases = FrozenDictionary<string, string>.Empty;
    private FrozenDictionary<string, string> _savedAppCommandAliases = FrozenDictionary<string, string>.Empty;
    private Task? _aliasSaveTask;
    private bool _aliasSavePending;
    private bool _canSaveAppCommandAliases = true;

    internal string FilePath { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCommandAliasStore"/> class. Loads retained command aliases from the supplied file without diagnostic logging.</summary>
    internal AppCommandAliasStore(string filePath)
        : this(filePath, NullLogger<AppCommandAliasStore>.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AppCommandAliasStore"/> class. Loads retained command aliases and preserves an unreadable file by disabling writes for this store.</summary>
    internal AppCommandAliasStore(string filePath, MEL.ILogger<AppCommandAliasStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);

        FilePath = filePath;
        _logger = logger;
        LoadAppCommandAliases();
    }

    /// <summary>Gets the alias-file path in the current host settings directory.</summary>
    internal static string DefaultPath()
    {
        return Path.Combine(Utilities.BaseSettingsPath("Microsoft.CmdPal"), "apps.aliases.json");
    }

    /// <summary>Gets the current immutable alias map without scheduling persistence.</summary>
    /// <remarks>Keys preserve their spelling; an empty target marks an alias that must not resolve.</remarks>
    internal FrozenDictionary<string, string> GetSnapshot()
    {
        return Volatile.Read(ref _appCommandAliases);
    }

    /// <summary>Waits for the active writer to drain its queued and coalesced alias updates.</summary>
    /// <remarks>Save failures are logged by the writer; this method does not retry failed writes.</remarks>
    internal Task WaitForSavesAsync()
    {
        lock (_stateLock)
        {
            return _aliasSaveTask ?? Task.CompletedTask;
        }
    }

    /// <summary>Replaces the immutable data snapshot and queues its persistence.</summary>
    internal void SetSnapshot(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        var snapshot = aliases as FrozenDictionary<string, string> ?? aliases.ToFrozenDictionary(StringComparer.Ordinal);
        lock (_stateLock)
        {
            if (ReferenceEquals(_appCommandAliases, snapshot))
            {
                return;
            }

            Volatile.Write(ref _appCommandAliases, snapshot);
            QueueSaveUnderLock();
        }
    }

    private void QueueSaveUnderLock()
    {
        _aliasSavePending = true;
        _aliasSaveTask ??= Task.Run(SavePendingAliases);
    }

    private void SavePendingAliases()
    {
        while (true)
        {
            lock (_stateLock)
            {
                if (!_aliasSavePending)
                {
                    _aliasSaveTask = null;
                    return;
                }

                _aliasSavePending = false;
            }

            SaveAppCommandAliases();
        }
    }

    private void SaveAppCommandAliases()
    {
        var aliases = Volatile.Read(ref _appCommandAliases);
        if (!_canSaveAppCommandAliases
            || ReferenceEquals(aliases, _savedAppCommandAliases))
        {
            return;
        }

        try
        {
            AppDataFile.WriteJsonAtomically(
                FilePath,
                new AppDataJsonContext.CommandAliasesFile(aliases),
                AppDataJsonContext.Default.CommandAliases,
                _logger);
            _savedAppCommandAliases = aliases;
        }
        catch (Exception ex)
        {
            LogAppCommandAliasesSaveFailed(_logger, ex);
        }
    }

    private void LoadAppCommandAliases()
    {
        IReadOnlyDictionary<string, string>? commandAliases = null;
        try
        {
            var content = AppDataFile.ReadTextOrNull(FilePath, _logger);

            // An empty map is authoritative; preferences never supply aliases.
            if (content is not null)
            {
                var aliasFile = JsonSerializer.Deserialize(content, AppDataJsonContext.Default.CommandAliases);
                if (aliasFile?.AppCommandAliases is not { } savedAliases
                    || savedAliases.Any(alias => string.IsNullOrWhiteSpace(alias.Key)
                        || alias.Value is null))
                {
                    throw new JsonException("The Apps command alias file must contain a string-to-string AppCommandAliases object.");
                }

                commandAliases = savedAliases;
            }
        }
        catch (Exception ex)
        {
            // Preserve unreadable history for recovery on the next application start.
            _canSaveAppCommandAliases = false;
            LogAppCommandAliasesLoadFailed(_logger, ex);
            return;
        }

        var aliases = commandAliases?.Where(alias => alias.Key != alias.Value).ToFrozenDictionary(StringComparer.Ordinal)
            ?? FrozenDictionary<string, string>.Empty;
        Volatile.Write(ref _appCommandAliases, aliases);
        _savedAppCommandAliases = aliases;
    }

    /// <summary>Flushes outstanding alias writes before catalog shutdown completes.</summary>
    public void Dispose()
    {
        WaitForSavesAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Failed to load Apps command aliases.")]
    private static partial void LogAppCommandAliasesLoadFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Failed to save Apps command aliases.")]
    private static partial void LogAppCommandAliasesSaveFailed(MEL.ILogger logger, Exception exception);
}
