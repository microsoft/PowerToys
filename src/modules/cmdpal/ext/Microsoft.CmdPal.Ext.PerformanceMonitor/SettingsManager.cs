// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "performanceMonitor";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ToggleSetting _useNetworkSpeedInBytesPerSec = new(
        Namespaced(nameof(UseNetworkSpeedInBytesPerSec)),
        Resources.GetResource("Network_Speed_Unit_Setting_Title"),
        Resources.GetResource("Network_Speed_Unit_Setting_Description"),
        false);

    public bool UseNetworkSpeedInBytesPerSec => _useNetworkSpeedInBytesPerSec.Value;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_useNetworkSpeedInBytesPerSec);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
