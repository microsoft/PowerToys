// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default implementation of <see cref="ISettingsService"/>.
/// Handles loading, saving, migration, and change notification for <see cref="SettingsModel"/>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string DeprecatedHotkeyGoesHomeKey = "HotkeyGoesHome";

    private readonly IPersistenceService _persistence;
    private readonly IApplicationInfoService _appInfoService;
    private readonly string _filePath;
    private readonly Lock _saveLock = new();

    public SettingsService(IPersistenceService persistence, IApplicationInfoService appInfoService)
    {
        _persistence = persistence;
        _appInfoService = appInfoService;
        _filePath = SettingsJsonPath();
        _settings = _persistence.Load(_filePath, JsonSerializationContext.Default.SettingsModel);
        ApplyMigrations();
    }

    private SettingsModel _settings;

    /// <inheritdoc/>
    public SettingsModel Settings => Volatile.Read(ref _settings);

    /// <inheritdoc/>
    public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

    /// <inheritdoc/>
    public void Save(bool hotReload = true) => UpdateSettings(s => s, hotReload);

    /// <inheritdoc/>
    public void UpdateSettings(Func<SettingsModel, SettingsModel> transform, bool hotReload = true)
    {
        SettingsModel snapshot;
        SettingsModel updated;
        do
        {
            snapshot = Volatile.Read(ref _settings);
            updated = transform(snapshot);
        }
        while (Interlocked.CompareExchange(ref _settings, updated, snapshot) != snapshot);

#if DEBUG
        // Capture the caller outside the lock and exclude tracing from the timings.
        var saveCaller = new StackTrace(skipFrames: 1, fNeedFileInfo: true);
        var saveLockRequested = Stopwatch.GetTimestamp();
        TimeSpan saveLockWait;
        TimeSpan saveDuration;
#endif

        SettingsModel newSettings;
        lock (_saveLock)
        {
#if DEBUG
            var saveStarted = Stopwatch.GetTimestamp();
            saveLockWait = Stopwatch.GetElapsedTime(saveLockRequested, saveStarted);
#endif

            // Read after acquiring the lock so an older caller cannot save a stale snapshot last.
            newSettings = Volatile.Read(ref _settings);
            _persistence.Save(newSettings, _filePath, JsonSerializationContext.Default.SettingsModel);

#if DEBUG
            saveDuration = Stopwatch.GetElapsedTime(saveStarted);
#endif
        }

#if DEBUG
        Logger.LogDebug(
            $"Settings save attempt (thread {Environment.CurrentManagedThreadId}, hotReload={hotReload}): " +
            $"lock wait {saveLockWait.TotalMilliseconds:F2} ms, persistence {saveDuration.TotalMilliseconds:F2} ms." +
            $"{Environment.NewLine}{saveCaller}");
#endif

        if (hotReload)
        {
            SettingsChanged?.Invoke(this, newSettings);
        }
    }

    private string SettingsJsonPath()
    {
        var directory = _appInfoService.ConfigDirectory;
        return Path.Combine(directory, "settings.json");
    }

    private void ApplyMigrations()
    {
        var migratedAny = false;

        try
        {
            var jsonContent = File.Exists(_filePath) ? File.ReadAllText(_filePath) : null;
            if (jsonContent is not null && JsonNode.Parse(jsonContent) is JsonObject root)
            {
                migratedAny |= TryMigrate(
                    "Migration #1: HotkeyGoesHome (bool) -> AutoGoHomeInterval (TimeSpan)",
                    root,
                    ref _settings,
                    nameof(SettingsModel.AutoGoHomeInterval),
                    DeprecatedHotkeyGoesHomeKey,
                    (ref SettingsModel model, bool goesHome) => model = model with { AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan },
                    JsonSerializationContext.Default.Boolean);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Migration check failed: {ex}");
        }

        var normalizedSettings = _settings.NormalizePinnedCommands();
        if (!ReferenceEquals(normalizedSettings, _settings))
        {
            _settings = normalizedSettings;
            migratedAny = true;
        }

        if (migratedAny)
        {
            Save(hotReload: false);
        }
    }

    private delegate void MigrationApply<T>(ref SettingsModel model, T value);

    private static bool TryMigrate<T>(
        string migrationName,
        JsonObject root,
        ref SettingsModel model,
        string newKey,
        string oldKey,
        MigrationApply<T> apply,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        try
        {
            if (root.ContainsKey(newKey) && root[newKey] is not null)
            {
                return false;
            }

            if (root.TryGetPropertyValue(oldKey, out var oldNode) && oldNode is not null)
            {
                var value = oldNode.Deserialize(jsonTypeInfo);
                apply(ref model, value!);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during migration {migrationName}.", ex);
        }

        return false;
    }
}
