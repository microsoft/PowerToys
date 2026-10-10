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
    internal static string controller_no_buttons_pressed => Get(nameof(controller_no_buttons_pressed));
    internal static string shortcut_required => Get(nameof(shortcut_required));
    internal static string shortcut_modifier_key_required => Get(nameof(shortcut_modifier_key_required));
    internal static string shortcut_modifier_invalid => Get(nameof(shortcut_modifier_invalid));
    internal static string shortcut_key_invalid => Get(nameof(shortcut_key_invalid));
    internal static string shortcut_key_code_invalid => Get(nameof(shortcut_key_code_invalid));
    internal static string shortcut_reserved => Get(nameof(shortcut_reserved));
    internal static string profile_filename_invalid => Get(nameof(profile_filename_invalid));
    internal static string profile_already_exists => Get(nameof(profile_already_exists));
    internal static string profile_name_already_exists => Get(nameof(profile_name_already_exists));
    internal static string profile_combination_already_exists => Get(nameof(profile_combination_already_exists));
    internal static string profile_name_required => Get(nameof(profile_name_required));
    internal static string profile_name_required_error => Get(nameof(profile_name_required_error));
    internal static string profile_name_invalid => Get(nameof(profile_name_invalid));
    internal static string profile_name_exists => Get(nameof(profile_name_exists));
    internal static string profile_valid_name_required => Get(nameof(profile_valid_name_required));
    internal static string profile_renamed => Get(nameof(profile_renamed));
    internal static string profile_duplicated => Get(nameof(profile_duplicated));
    internal static string layout_empty => Get(nameof(layout_empty));
    internal static string layout_missing_displays => Get(nameof(layout_missing_displays));
    internal static string layout_invalid_targets => Get(nameof(layout_invalid_targets));
    internal static string layout_invalid_display => Get(nameof(layout_invalid_display));
    internal static string layout_duplicate_device => Get(nameof(layout_duplicate_device));
    internal static string layout_primary_count => Get(nameof(layout_primary_count));
    internal static string layout_overlap => Get(nameof(layout_overlap));
    internal static string progress_checking_displays => Get(nameof(progress_checking_displays));
    internal static string progress_activating_displays => Get(nameof(progress_activating_displays));
    internal static string progress_activating_profile => Get(nameof(progress_activating_profile));
    internal static string query_buffer_sizes_failed => Get(nameof(query_buffer_sizes_failed));
    internal static string query_topology_failed => Get(nameof(query_topology_failed));
    internal static string query_topology_changed => Get(nameof(query_topology_changed));
    internal static string capture_topology_failed => Get(nameof(capture_topology_failed));
    internal static string recovery_result_format => Get(nameof(recovery_result_format));
    internal static string win32_invalid_parameters => Get(nameof(win32_invalid_parameters));
    internal static string win32_not_supported => Get(nameof(win32_not_supported));
    internal static string win32_access_denied => Get(nameof(win32_access_denied));
    internal static string win32_not_enough_memory => Get(nameof(win32_not_enough_memory));
    internal static string win32_incorrect_parameter => Get(nameof(win32_incorrect_parameter));
    internal static string win32_invalid_name => Get(nameof(win32_invalid_name));
    internal static string win32_no_more_items => Get(nameof(win32_no_more_items));
    internal static string win32_timeout => Get(nameof(win32_timeout));
    internal static string win32_unknown_error => Get(nameof(win32_unknown_error));

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}

#pragma warning restore SA1300, SA1516
