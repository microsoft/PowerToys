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
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>Loads and persists explicitly hidden identities independently of catalog rules.</summary>
/// <remarks>The catalog serializes snapshot updates; the store serializes file writes.</remarks>
internal sealed partial class AppVisibilityStore : IAppVisibilityStore
{
    private readonly Lock _fileLock = new();
    private readonly MEL.ILogger<AppVisibilityStore> _logger;
    private FrozenSet<string> _hiddenAppIdentities = FrozenSet<string>.Empty;
    private bool _canSave = true;

    internal string FilePath { get; }

    /// <summary>Initializes a new instance of the <see cref="AppVisibilityStore"/> class. Loads raw hidden identities from the supplied file and preserves unreadable data against later writes.</summary>
    internal AppVisibilityStore(string filePath, MEL.ILogger<AppVisibilityStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        _logger = logger ?? NullLogger<AppVisibilityStore>.Instance;
        Load();
    }

    /// <summary>Gets the visibility-file path in the current host settings directory.</summary>
    internal static string DefaultPath()
    {
        return Path.Combine(Utilities.BaseSettingsPath("Microsoft.CmdPal"), "apps.visibility.json");
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetSnapshot()
    {
        return Volatile.Read(ref _hiddenAppIdentities);
    }

    /// <inheritdoc />
    public bool SetSnapshot(IReadOnlySet<string> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ThrowIfUnreadable();
        if (_hiddenAppIdentities.SetEquals(identities))
        {
            return false;
        }

        Volatile.Write(ref _hiddenAppIdentities, identities.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
        return true;
    }

    /// <inheritdoc />
    public void Persist()
    {
        lock (_fileLock)
        {
            ThrowIfUnreadable();
            var identities = Volatile.Read(ref _hiddenAppIdentities).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            AppDataFile.WriteJsonAtomically(
                FilePath,
                new AppDataJsonContext.VisibilityFile(identities),
                AppDataJsonContext.Default.Visibility,
                _logger);
        }
    }

    private void Load()
    {
        try
        {
            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var content = AppDataFile.ReadTextOrNull(FilePath, _logger);
            if (content is not null)
            {
                var data = JsonSerializer.Deserialize(content, AppDataJsonContext.Default.Visibility);
                if (data?.HiddenAppIdentities is not { } identities || identities.Any(string.IsNullOrWhiteSpace))
                {
                    throw new JsonException("The Apps visibility file must contain a HiddenAppIdentities array.");
                }

                hidden.UnionWith(identities);
            }

            _hiddenAppIdentities = hidden.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _canSave = false;
            LogVisibilityLoadFailed(_logger, ex);
        }
    }

    private void ThrowIfUnreadable()
    {
        if (!_canSave)
        {
            throw new InvalidOperationException("Cannot change an unreadable Apps visibility file. Correct the read failure and restart Command Palette.");
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to load Apps visibility preferences.")]
    private static partial void LogVisibilityLoadFailed(MEL.ILogger logger, Exception exception);
}
