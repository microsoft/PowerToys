// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace MonitorPowerCore;

#pragma warning disable SA1300, SA1516

internal static class Resources
{
    private static readonly ResourceManager ResourceManager = new("MonitorPowerCore.Resources", Assembly.GetExecutingAssembly());

    internal static string activated_displays_format => Get(nameof(activated_displays_format));
    internal static string display_label => Get(nameof(display_label));
    internal static string display_state_restored => Get(nameof(display_state_restored));
    internal static string error_format => Get(nameof(error_format));
    internal static string error_no_active_monitors_remaining => Get(nameof(error_no_active_monitors_remaining));
    internal static string error_no_displays_connected => Get(nameof(error_no_displays_connected));
    internal static string error_no_displays_selected => Get(nameof(error_no_displays_selected));
    internal static string error_no_matching_paths => Get(nameof(error_no_matching_paths));
    internal static string error_primary_display_not_found => Get(nameof(error_primary_display_not_found));
    internal static string error_prefix => Get(nameof(error_prefix));
    internal static string error_profile_empty => Get(nameof(error_profile_empty));
    internal static string error_profile_layout_invalid => Get(nameof(error_profile_layout_invalid));
    internal static string error_profile_not_found => Get(nameof(error_profile_not_found));
    internal static string error_saved_state_no_paths => Get(nameof(error_saved_state_no_paths));
    internal static string no_active_displays => Get(nameof(no_active_displays));
    internal static string no_saved_state => Get(nameof(no_saved_state));
    internal static string profile_saved => Get(nameof(profile_saved));
    internal static string restore_failed_format => Get(nameof(restore_failed_format));
    internal static string switched_display_topology => Get(nameof(switched_display_topology));
    internal static string unknown_display => Get(nameof(unknown_display));

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}

#pragma warning restore SA1300, SA1516
