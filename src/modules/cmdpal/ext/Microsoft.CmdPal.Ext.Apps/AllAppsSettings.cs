// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public class AllAppsSettings : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "apps";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _searchResultLimitChoices =
    [
        new ChoiceSetSetting.Choice(Resources.limit_0, "0"),
        new ChoiceSetSetting.Choice(Resources.limit_1, "1"),
        new ChoiceSetSetting.Choice(Resources.limit_5, "5"),
        new ChoiceSetSetting.Choice(Resources.limit_10, "10"),
    ];

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

    private readonly ChoiceSetSetting _searchResultLimitSource = new(
        Namespaced(nameof(SearchResultLimit)),
        Resources.limit_fallback_results_source,
        Resources.limit_fallback_results_source_description,
        _searchResultLimitChoices);

    public string SearchResultLimit => _searchResultLimitSource.Value ?? string.Empty;

    private readonly ToggleSetting _enableStartMenuSource = new(
        Namespaced(nameof(EnableStartMenuSource)),
        Resources.enable_start_menu_source,
        string.Empty,
        true);

    private readonly ToggleSetting _enableDesktopSource = new(
        Namespaced(nameof(EnableDesktopSource)),
        Resources.enable_desktop_source,
        string.Empty,
        true);

    private readonly ToggleSetting _enableRegistrySource = new(
        Namespaced(nameof(EnableRegistrySource)),
        Resources.enable_registry_source,
        string.Empty,
        false); // This one is very noisy

    private readonly ToggleSetting _enablePathEnvironmentVariableSource = new(
        Namespaced(nameof(EnablePathEnvironmentVariableSource)),
        Resources.enable_path_environment_variable_source,
        string.Empty,
        false); // this one is very VERY noisy

    public double MinScoreThreshold { get; set; } = 0.75;

    internal const char SuffixSeparator = ';';

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
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
        Settings.Add(_searchResultLimitSource);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
