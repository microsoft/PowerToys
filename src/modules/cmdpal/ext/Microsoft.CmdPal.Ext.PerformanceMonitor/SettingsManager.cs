// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "performanceMonitor";
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly Lock _fileLock = new();
    private string _defaultNetworkAdapterId = NetworkStats.AllPhysicalAdaptersId;

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ChoiceSetSetting _networkSpeedUnit = new(
        Namespaced(nameof(NetworkSpeedUnit)),
        Resources.GetResource("Network_Speed_Unit_Setting_Title"),
        Resources.GetResource("Network_Speed_Unit_Setting_Description"),
        [
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BitsPerSec"), SpeedUnit.BitsPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BytesPerSec"), SpeedUnit.BytesPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BinaryBytesPerSec"), SpeedUnit.BinaryBytesPerSecond.ToString("G")),
        ]);

    public SpeedUnit NetworkSpeedUnit =>
        Enum.TryParse<SpeedUnit>(_networkSpeedUnit.Value, out var unit)
            ? unit
            : SpeedUnit.BitsPerSecond;

    public string DefaultNetworkAdapterId => _defaultNetworkAdapterId;

    private readonly ChoiceSetSetting _diskSpeedUnit = new(
        Namespaced(nameof(DiskSpeedUnit)),
        Resources.GetResource("Disk_Speed_Unit_Setting_Title"),
        Resources.GetResource("Disk_Speed_Unit_Setting_Description"),
        [
            new ChoiceSetSetting.Choice(Resources.GetResource("Disk_Speed_Unit_BitsPerSec"), SpeedUnit.BitsPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Disk_Speed_Unit_BytesPerSec"), SpeedUnit.BytesPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Disk_Speed_Unit_BinaryBytesPerSec"), SpeedUnit.BinaryBytesPerSecond.ToString("G")),
        ]);

    public SpeedUnit DiskSpeedUnit =>
        Enum.TryParse<SpeedUnit>(_diskSpeedUnit.Value, out var unit)
            ? unit
            : SpeedUnit.BytesPerSecond;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Namespace}.settings.json");
    }

    public SettingsManager()
        : this(SettingsJsonPath())
    {
    }

    internal SettingsManager(string filePath)
    {
        FilePath = filePath;

        Settings.Add(_networkSpeedUnit);
        Settings.Add(_diskSpeedUnit);

        LoadSettings();
        LoadDefaultNetworkAdapterId();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public void SetDefaultNetworkAdapterId(string adapterId)
    {
        if (string.IsNullOrWhiteSpace(adapterId) || string.Equals(_defaultNetworkAdapterId, adapterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _defaultNetworkAdapterId = adapterId;
        SaveSettings();
    }

    public override void SaveSettings()
    {
        lock (_fileLock)
        {
            base.SaveSettings();
            SaveDefaultNetworkAdapterId();
        }
    }

    private void LoadDefaultNetworkAdapterId()
    {
        try
        {
            if (File.Exists(FilePath) && JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject savedSettings)
            {
                var value = savedSettings[Namespaced(nameof(DefaultNetworkAdapterId))]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _defaultNetworkAdapterId = value;
                }
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to load the default network adapter setting.", ex);
        }
    }

    private void SaveDefaultNetworkAdapterId()
    {
        try
        {
            var savedSettings = File.Exists(FilePath)
                ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject
                : new JsonObject();

            if (savedSettings is null)
            {
                CoreLogger.LogError("Failed to parse the performance monitor settings file as a JSON object.");
                return;
            }

            savedSettings[Namespaced(nameof(DefaultNetworkAdapterId))] = _defaultNetworkAdapterId;
            File.WriteAllText(FilePath, savedSettings.ToJsonString(_serializerOptions));
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to save the default network adapter setting.", ex);
        }
    }
}
