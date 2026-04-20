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

        var newSettings = Volatile.Read(ref _settings);
        _persistence.Save(newSettings, _filePath, JsonSerializationContext.Default.SettingsModel);
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

                migratedAny |= TryMigrateBandShowLabels(root, ref _settings);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Migration check failed: {ex}");
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

    /// <summary>
    /// Migrates per-band <c>ShowLabels</c> to <c>ShowTitles</c> and <c>ShowSubtitles</c>.
    /// The old <c>ShowLabels</c> property on <see cref="DockBandSettings"/> was renamed to
    /// <c>ShowTitles</c> (with <c>ShowSubtitles</c> added). Because the legacy property is
    /// <c>[JsonIgnore]</c>, old JSON values are lost during deserialization. This migration
    /// reads the raw JSON to recover them.
    /// </summary>
    private static bool TryMigrateBandShowLabels(JsonObject root, ref SettingsModel model)
    {
        try
        {
            if (root[nameof(SettingsModel.DockSettings)] is not JsonObject dockSettingsNode)
            {
                return false;
            }

            var migrated = false;
            var ds = model.DockSettings;

            var newStart = MigrateBandList(dockSettingsNode, nameof(DockSettings.StartBands), ds.StartBands, ref migrated);
            var newCenter = MigrateBandList(dockSettingsNode, nameof(DockSettings.CenterBands), ds.CenterBands, ref migrated);
            var newEnd = MigrateBandList(dockSettingsNode, nameof(DockSettings.EndBands), ds.EndBands, ref migrated);

            if (migrated)
            {
                model = model with
                {
                    DockSettings = ds with
                    {
                        StartBands = newStart,
                        CenterBands = newCenter,
                        EndBands = newEnd,
                    },
                };
            }

            return migrated;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during band ShowLabels migration.", ex);
            return false;
        }
    }

    /// <summary>
    /// Scans a single band array in the raw JSON for <c>ShowLabels</c> entries that
    /// need migrating to <c>ShowTitles</c> / <c>ShowSubtitles</c>.
    /// </summary>
    private static ImmutableList<DockBandSettings> MigrateBandList(
        JsonObject dockSettingsNode,
        string bandKey,
        ImmutableList<DockBandSettings> bands,
        ref bool anyMigrated)
    {
        if (dockSettingsNode[bandKey] is not JsonArray jsonBands)
        {
            return bands;
        }

        var builder = bands.ToBuilder();
        var listChanged = false;

        for (var i = 0; i < builder.Count && i < jsonBands.Count; i++)
        {
            if (jsonBands[i] is not JsonObject jsonBand)
            {
                continue;
            }

            // Only migrate if old key exists and new key does not
            if (!jsonBand.ContainsKey("ShowLabels") || jsonBand.ContainsKey("ShowTitles"))
            {
                continue;
            }

            var showLabelsNode = jsonBand["ShowLabels"];
            if (showLabelsNode is null)
            {
                continue;
            }

            var showLabels = showLabelsNode.GetValue<bool>();
            var band = builder[i];
            band = band with
            {
                ShowTitles = band.ShowTitles ?? showLabels,
                ShowSubtitles = band.ShowSubtitles ?? showLabels,
            };
            builder[i] = band;
            listChanged = true;
        }

        if (listChanged)
        {
            anyMigrated = true;
            return builder.ToImmutable();
        }

        return bands;
    }
}
