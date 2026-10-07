// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public class AllAppsSettings : JsonSettingsManager
{
    private const int DefaultSearchResultLimit = 10;
    private const ExecutableNameMatchMode DefaultExecutableNameMatchMode = ExecutableNameMatchMode.FilenameOnly;

    // "none" instead of "0": the original default was accidentally "0", so existing
    // users may have "0" stored. Using "none" lets us distinguish intentional "show
    // no results" from the old accidental default (which is now treated as "use default").
    private const string NoneResultLimitValue = "none";

    private static readonly CompositeFormat DefaultLimitItemTitleFormat = CompositeFormat.Parse(Resources.limit_default);
    private static readonly string DefaultLimitItemTitle = string.Format(
        CultureInfo.CurrentCulture,
        DefaultLimitItemTitleFormat.Format,
        DefaultSearchResultLimit);

    private static readonly string _namespace = "apps";

    private static readonly List<ChoiceSetSetting.Choice> _searchResultLimitChoices =
    [
        new(DefaultLimitItemTitle, "-1"),
        new(Resources.limit_0, NoneResultLimitValue),
        new(Resources.limit_1, "1"),
        new(Resources.limit_5, "5"),
        new(Resources.limit_10, "10"),
    ];

    internal const char SuffixSeparator = ';';

    private readonly ChoiceSetSetting _searchResultLimitSource = new(
        Namespaced(nameof(SearchResultLimit)),
        Resources.limit_fallback_results_source,
        Resources.limit_fallback_results_source_description,
        _searchResultLimitChoices)
    {
        IgnoreUnknownValue = true,
    };

    private readonly ChoiceSetSetting _executableNameMatchMode = new(
        Namespaced(nameof(ExecutableNameMatchMode)),
        Resources.executable_name_match_mode,
        Resources.executable_name_match_mode_description,
        [
            new(Resources.executable_name_match_mode_default, "default"),
            new(Resources.executable_name_match_mode_filename_and_stem, "filenameAndStem"),
            new(Resources.executable_name_match_mode_filename_only, "filenameOnly"),
            new(Resources.executable_name_match_mode_disabled, "disabled"),
        ])
    {
        IgnoreUnknownValue = true,
    };

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

    private readonly ToggleSetting _hideAppDescriptions = new(
        Namespaced(nameof(HideAppDescriptions)),
        Resources.hide_app_descriptions,
        Resources.hide_app_descriptions_description,
        false);

    private readonly ToggleSetting _hideUninstallers = new(
        Namespaced(nameof(HideUninstallers)),
        Resources.hide_uninstallers,
        Resources.hide_uninstallers_description,
        false);

    private readonly ToggleSetting _enableCatalogDiagnostics = new(
        Namespaced(nameof(EnableCatalogDiagnostics)),
        Resources.enable_catalog_diagnostics,
        Resources.enable_catalog_diagnostics_description,
        false);

    private readonly StringListSetting _excludedAppNames = new(
        Namespaced(nameof(ExcludedAppNames)),
        Resources.excluded_app_names,
        Resources.excluded_app_names_description,
        []);

    private readonly StringListSetting _excludedAppPaths = new(
        Namespaced(nameof(ExcludedAppPaths)),
        Resources.excluded_app_paths,
        Resources.excluded_app_paths_description,
        []);

    private readonly FilePathListSetting _customShortcutFolders = new(
        Namespaced(nameof(CustomShortcutFolders)),
        Resources.custom_shortcut_folders,
        Resources.custom_shortcut_folders_description,
        [],
        FilePathListItemType.Folders)
    {
        PreventDuplicates = true,
        DuplicateItemErrorMessage = Resources.custom_app_folder_duplicate,
    };

    private readonly FilePathListSetting _portableAppFolders = new(
        Namespaced(nameof(PortableAppFolders)),
        Resources.portable_app_folders,
        Resources.portable_app_folders_description,
        [],
        FilePathListItemType.Folders)
    {
        PreventDuplicates = true,
        DuplicateItemErrorMessage = Resources.custom_app_folder_duplicate,
    };

    public List<string> ProgramSuffixes { get; set; } = ["bat", "appref-ms", "exe", "lnk", "url"];

    public List<string> RunCommandSuffixes { get; set; } = ["bat", "appref-ms", "exe", "lnk", "url", "cpl", "msc"];

    public bool EnableStartMenuSource => _enableStartMenuSource.Value;

    public bool EnableDesktopSource => _enableDesktopSource.Value;

    public bool EnableRegistrySource => _enableRegistrySource.Value;

    public bool EnablePathEnvironmentVariableSource => _enablePathEnvironmentVariableSource.Value;

    public bool IncludeNonAppsOnDesktop => _includeNonAppsOnDesktop.Value;

    public bool IncludeNonAppsInStartMenu => _includeNonAppsInStartMenu.Value;

    public bool HideAppDescriptions => _hideAppDescriptions.Value;

    public bool HideUninstallers => _hideUninstallers.Value;

    public bool EnableCatalogDiagnostics => _enableCatalogDiagnostics.Value;

    /// <summary>Gets when exact executable names receive priority in All Apps and Home search, resolving the default policy.</summary>
    public ExecutableNameMatchMode ExecutableNameMatchMode => _executableNameMatchMode.Value switch
    {
        "default" => DefaultExecutableNameMatchMode,
        "disabled" => ExecutableNameMatchMode.Disabled,
        "filenameOnly" => ExecutableNameMatchMode.FilenameOnly,
        "filenameAndStem" => ExecutableNameMatchMode.FilenameAndStem,
        _ => DefaultExecutableNameMatchMode,
    };

    public IReadOnlyList<string> ExcludedAppNames => _excludedAppNames.Value ?? [];

    public IReadOnlyList<string> ExcludedAppPaths => _excludedAppPaths.Value ?? [];

    /// <summary>Gets user-selected folders whose application shortcuts should be indexed recursively.</summary>
    public IReadOnlyList<string> CustomShortcutFolders => _customShortcutFolders.Value ?? [];

    /// <summary>Gets user-selected folders whose portable executable applications should be indexed.</summary>
    public IReadOnlyList<string> PortableAppFolders => _portableAppFolders.Value ?? [];

    /// <summary>
    /// Gets the parsed search result limit. Returns <see langword="null"/> when the caller should
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

    /// <summary>Gets the configured result limit, or the built-in default when no override is set.</summary>
    public int EffectiveSearchResultLimit => SearchResultLimit ?? DefaultSearchResultLimit;

    /// <summary>Initializes Apps preferences from the default settings path.</summary>
    public AllAppsSettings()
        : this(SettingsJsonPath())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AllAppsSettings"/> class. Registers Apps preferences, loads the supplied settings file, and saves subsequent preference changes.</summary>
    internal AllAppsSettings(string filePath)
    {
        FilePath = filePath;

        Settings.Add(_enableStartMenuSource);
        Settings.Add(_includeNonAppsInStartMenu);
        Settings.Add(_enableDesktopSource);
        Settings.Add(_includeNonAppsOnDesktop);
        Settings.Add(_enableRegistrySource);
        Settings.Add(_enablePathEnvironmentVariableSource);
        Settings.Add(_customShortcutFolders);
        Settings.Add(_portableAppFolders);
        Settings.Add(_searchResultLimitSource);
        Settings.Add(_executableNameMatchMode);
        Settings.Add(_hideAppDescriptions);
        Settings.Add(_hideUninstallers);
        Settings.Add(_excludedAppNames);
        Settings.Add(_excludedAppPaths);
        Settings.Add(_enableCatalogDiagnostics);

        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    private static string Namespaced(string propertyName)
    {
        return $"{_namespace}.{propertyName}";
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }
}
