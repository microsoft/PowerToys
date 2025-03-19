// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public class AllAppsSettings : JsonSettingsManager
{
    private static readonly string _namespace = "apps";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static string Experimental(string propertyName) => $"{_namespace}.experimental.{propertyName}";

#pragma warning disable SA1401 // Fields should be private
    internal static AllAppsSettings Instance = new();
#pragma warning restore SA1401 // Fields should be private

    public DateTime LastIndexTime { get; set; }

    public List<ProgramSource> ProgramSources { get; set; } = [];

    public List<DisabledProgramSource> DisabledProgramSources { get; set; } = [];

    public List<string> ProgramSuffixes { get; set; } = ["bat", "appref-ms", "exe", "lnk", "url"];

    public List<string> RunCommandSuffixes { get; set; } = ["bat", "appref-ms", "exe", "lnk", "url", "cpl", "msc"];

    public bool EnableStartMenuSource => _enableStartMenuSource.Value;

    public bool EnableDesktopSource => _enableDesktopSource.Value;

    public bool EnableRegistrySource => _enableRegistrySource.Value;

    public bool EnablePathEnvironmentVariableSource => _enablePathEnvironmentVariableSource.Value;

    private readonly ToggleSetting _enableStartMenuSource = new(
        Namespaced(nameof(EnableStartMenuSource)),
        Resources.enable_start_menu_source,
        Resources.enable_start_menu_source,
        true);

    private readonly ToggleSetting _enableDesktopSource = new(
        Namespaced(nameof(EnableDesktopSource)),
        Resources.enable_desktop_source,
        Resources.enable_desktop_source,
        true);

    private readonly ToggleSetting _enableRegistrySource = new(
        Namespaced(nameof(EnableRegistrySource)),
        Resources.enable_registry_source,
        Resources.enable_registry_source,
        false); // This one is very noisy

    private readonly ToggleSetting _enablePathEnvironmentVariableSource = new(
        Namespaced(nameof(EnablePathEnvironmentVariableSource)),
        Resources.enable_path_environment_variable_source,
        Resources.enable_path_environment_variable_source,
        false); // this one is very VERY noisy

    public double MinScoreThreshold { get; set; } = 0.75;

    internal const char SuffixSeparator = ';';

    internal static string SettingsJsonPath()
    {
        string directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public AllAppsSettings()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_enableStartMenuSource);
        Settings.Add(_enableDesktopSource);
        Settings.Add(_enableRegistrySource);
        Settings.Add(_enablePathEnvironmentVariableSource);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
