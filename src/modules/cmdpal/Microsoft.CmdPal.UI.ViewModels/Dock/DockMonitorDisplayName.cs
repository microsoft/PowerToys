// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// Resolves the same persistent display name for dock settings and monitor labels.
/// </summary>
public static class DockMonitorDisplayName
{
    private static readonly CompositeFormat DisplayNameFormat = CompositeFormat.Parse(Properties.Resources.dock_monitor_display_name);
    private static readonly CompositeFormat PrimaryDisplayNameFormat = CompositeFormat.Parse(Properties.Resources.dock_monitor_primary_display_name);

    public static string Resolve(MonitorInfo monitor, DockMonitorConfig? config)
    {
        var name = config?.DisplayNameOverride;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = ResolveDefaultName(monitor, config);
        }

        // Primary status is current topology information, not part of the saved name.
        return monitor.IsPrimary
            ? string.Format(CultureInfo.CurrentCulture, PrimaryDisplayNameFormat, name)
            : name;
    }

    /// <summary>
    /// Gets the automatic name without a user override or primary-display suffix.
    /// </summary>
    public static string ResolveDefaultName(MonitorInfo monitor, DockMonitorConfig? config)
    {
        var name = monitor.FriendlyName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = config is { FallbackDisplayNumber: > 0 }
                ? string.Format(CultureInfo.CurrentCulture, DisplayNameFormat, FormatLabel(config.FallbackDisplayNumber))
                : Properties.Resources.dock_monitor_display_name_default;
        }

        return name;
    }

    private static string FormatLabel(int number)
    {
        var label = string.Empty;
        while (number > 0)
        {
            number--;
            label = (char)('A' + (number % 26)) + label;
            number /= 26;
        }

        return label;
    }
}
