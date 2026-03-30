// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public class AllAppsSettings : JsonSettingsManager, ISettingsInterface
{
    // "none" instead of "0": the original default was accidentally "0", so existing
    // users may have "0" stored. Using "none" lets us distinguish intentional "show
    // no results" from the old accidental default (which is now treated as "use default").
    private const string NoneResultLimitValue = "none";

    private static readonly CompositeFormat DefaultLimitItemTitleFormat = CompositeFormat.Parse(Resources.limit_default);
    private static readonly string DefaultLimitItemTitle = string.Format(
        CultureInfo.CurrentCulture,
        DefaultLimitItemTitleFormat.Format,
        AllAppsCommandProvider.DefaultResultLimit);

    private static readonly string _namespace = "apps";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _searchResultLimitChoices =
    [
        new(DefaultLimitItemTitle, "-1"),
        new(Resources.limit_0, NoneResultLimitValue),
        new(Resources.limit_1, "1"),
        new(Resources.limit_5, "5"),
        new(Resources.limit_10, "10"),
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

    public bool IncludeNonAppsOnDesktop => _includeNonAppsOnDesktop.Value;

    public bool IncludeNonAppsInStartMenu => _includeNonAppsInStartMenu.Value;

    private readonly ChoiceSetSetting _searchResultLimitSource = new(
        Namespaced(nameof(SearchResultLimit)),
        Resources.limit_fallback_results_source,
        Resources.limit_fallback_results_source_description,
        _searchResultLimitChoices)
    {
        IgnoreUnknownValue = true,
    };

    /// <summary>
    /// Parsed search result limit. Returns <see langword="null"/> when the caller should
    /// use its own default (unrecognized value, empty, or old stored "0").
    /// </summary>
    public int? SearchResultLimit
    {
        get
        {
            var raw = _searchResultLimitSource.Value ?? string.Empty;

            if (string.Equals(raw, NoneResultLimitValue, StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(raw)
                || !int.TryParse(raw, out var result)
                || result <= 0) //// <= 0: treats old stored "0" as "use default"
            {
                return null;
            }

            return result;
        }
    }

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

    private readonly ToggleSetting _includeNonAppsOnDesktop = new(
        Namespaced(nameof(IncludeNonAppsOnDesktop)),
        Resources.include_non_apps_on_desktop,
        string.Empty,
        false);

    private readonly ToggleSetting _includeNonAppsInStartMenu = new(
        Namespaced(nameof(IncludeNonAppsInStartMenu)),
        Resources.include_non_apps_in_start_menu,
        string.Empty,
        true);

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
        Settings.Add(_includeNonAppsInStartMenu);
        Settings.Add(_enableDesktopSource);
        Settings.Add(_includeNonAppsOnDesktop);
        Settings.Add(_enableRegistrySource);
        Settings.Add(_enablePathEnvironmentVariableSource);
        Settings.Add(_searchResultLimitSource);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
