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
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BitsPerSec"), NetworkSpeedUnit.BitsPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BytesPerSec"), NetworkSpeedUnit.BytesPerSecond.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.GetResource("Network_Speed_Unit_BinaryBytesPerSec"), NetworkSpeedUnit.BinaryBytesPerSecond.ToString("G")),
        ]);

    public NetworkSpeedUnit NetworkSpeedUnit =>
        Enum.TryParse<NetworkSpeedUnit>(_networkSpeedUnit.Value, out var unit)
            ? unit
            : NetworkSpeedUnit.BitsPerSecond;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_networkSpeedUnit);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
