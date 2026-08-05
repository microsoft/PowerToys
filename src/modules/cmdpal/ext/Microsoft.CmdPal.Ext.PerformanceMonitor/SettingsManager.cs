// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "performanceMonitor";

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
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_networkSpeedUnit);
        Settings.Add(_diskSpeedUnit);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
