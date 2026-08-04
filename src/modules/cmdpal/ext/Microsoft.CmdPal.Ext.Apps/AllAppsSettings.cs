// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps;

public partial class AllAppsSettings : JsonSettingsManager
{
    private const int DefaultSearchResultLimit = 10;
    private const string DisabledProgramSourcesPropertyName = "DisabledProgramSources";
    private const string DisabledProgramUniqueIdentifierPropertyName = "UniqueIdentifier";

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

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _searchResultLimitChoices =
    [
        new(DefaultLimitItemTitle, "-1"),
        new(Resources.limit_0, NoneResultLimitValue),
        new(Resources.limit_1, "1"),
        new(Resources.limit_5, "5"),
        new(Resources.limit_10, "10"),
    ];

    private readonly Lock _settingsWriterLock = new();
    private readonly MEL.ILogger<AllAppsSettings> _logger;
    private FrozenSet<string> _disabledProgramIdentities = FrozenSet<string>.Empty;

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

    public IReadOnlyList<string> ExcludedAppNames => _excludedAppNames.Value ?? [];

    public IReadOnlyList<string> ExcludedAppPaths => _excludedAppPaths.Value ?? [];

    /// <summary>Gets user-selected folders whose application shortcuts should be indexed recursively.</summary>
    public IReadOnlyList<string> CustomShortcutFolders => _customShortcutFolders.Value ?? [];

    /// <summary>Gets user-selected folders whose portable executable applications should be indexed.</summary>
    public IReadOnlyList<string> PortableAppFolders => _portableAppFolders.Value ?? [];

    private readonly ChoiceSetSetting _searchResultLimitSource = new(
        Namespaced(nameof(SearchResultLimit)),
        Resources.limit_fallback_results_source,
        Resources.limit_fallback_results_source_description,
        _searchResultLimitChoices)
    {
        IgnoreUnknownValue = true,
    };

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

    internal const char SuffixSeparator = ';';

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    /// <summary>
    /// Initializes the process settings using the injected application logger.
    /// </summary>
    /// <param name="logger">The diagnostic logger; messages sent here do not enter the extension UI log.</param>
    public AllAppsSettings(MEL.ILogger<AllAppsSettings> logger)
        : this(SettingsJsonPath(), logger)
    {
    }

    internal AllAppsSettings(string filePath)
        : this(filePath, NullLogger<AllAppsSettings>.Instance)
    {
    }

    internal AllAppsSettings(string filePath, MEL.ILogger<AllAppsSettings> logger)
    {
        FilePath = filePath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Settings.Add(_enableStartMenuSource);
        Settings.Add(_includeNonAppsInStartMenu);
        Settings.Add(_enableDesktopSource);
        Settings.Add(_includeNonAppsOnDesktop);
        Settings.Add(_enableRegistrySource);
        Settings.Add(_enablePathEnvironmentVariableSource);
        Settings.Add(_customShortcutFolders);
        Settings.Add(_portableAppFolders);
        Settings.Add(_searchResultLimitSource);
        Settings.Add(_hideAppDescriptions);
        Settings.Add(_hideUninstallers);
        Settings.Add(_excludedAppNames);
        Settings.Add(_excludedAppPaths);
        Settings.Add(_enableCatalogDiagnostics);

        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    public override void LoadSettings()
    {
        lock (_settingsWriterLock)
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(LoadSettings)}");
            }

            if (!File.Exists(FilePath))
            {
                return;
            }

            try
            {
                var content = File.ReadAllText(FilePath);
                if (JsonNode.Parse(content) is not JsonObject savedSettings)
                {
                    LogInvalidSettingsJson(_logger);
                    return;
                }

                LoadDisabledProgramSources(savedSettings);
                Settings.Update(content);
            }
            catch (Exception ex)
            {
                LogSettingsLoadFailed(_logger, ex);
            }
        }
    }

    public override void SaveSettings()
    {
        lock (_settingsWriterLock)
        {
            SaveSettingsUnderLock();
        }
    }

    /// <summary>Determines whether a canonical application identity is hidden.</summary>
    internal bool IsAppHidden(string identity) =>
        Volatile.Read(ref _disabledProgramIdentities).Contains(identity);

    /// <summary>Updates the in-memory visibility preference for a canonical application identity.</summary>
    internal bool SetAppHidden(string identity, bool hidden)
    {
        lock (_settingsWriterLock)
        {
            var current = _disabledProgramIdentities;
            if (current.Contains(identity) == hidden)
            {
                return false;
            }

            var updated = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            _ = hidden ? updated.Add(identity) : updated.Remove(identity);
            Volatile.Write(ref _disabledProgramIdentities, updated.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
            return true;
        }
    }

    private void SaveSettingsUnderLock()
    {
        string? temporaryPath = null;

        try
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(SaveSettings)}");
            }

            if (JsonNode.Parse(Settings.ToJson()) is not JsonObject currentSettings)
            {
                LogSettingsSerializationFailed(_logger);
                return;
            }

            var previousContent = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "{}";
            if (JsonNode.Parse(previousContent) is not JsonObject savedSettings)
            {
                LogInvalidSettingsJson(_logger);
                return;
            }

            foreach (var setting in currentSettings)
            {
                savedSettings[setting.Key] = setting.Value?.DeepClone();
            }

            var hiddenApps = new JsonArray();
            foreach (var hiddenIdentity in _disabledProgramIdentities.Order(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(hiddenIdentity))
                {
                    hiddenApps.Add((JsonNode)new JsonObject
                    {
                        [DisabledProgramUniqueIdentifierPropertyName] = hiddenIdentity,
                    });
                }
            }

            savedSettings[DisabledProgramSourcesPropertyName] = hiddenApps;

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, savedSettings.ToJsonString(_serializerOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            LogSettingsSaveFailed(_logger, ex);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogTemporarySettingsFileRemovalFailed(_logger, ex);
                }
            }
        }
    }

    private void LoadDisabledProgramSources(JsonObject savedSettings)
    {
        var hiddenIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (savedSettings[DisabledProgramSourcesPropertyName] is JsonArray hiddenApps)
        {
            foreach (var node in hiddenApps)
            {
                string? identity = null;
                if (node is JsonObject hiddenApp
                    && hiddenApp[DisabledProgramUniqueIdentifierPropertyName] is JsonValue identityValue
                    && identityValue.TryGetValue<string>(out var persistedIdentity))
                {
                    identity = persistedIdentity;
                }
                else if (node is JsonValue value && value.TryGetValue<string>(out var legacyIdentity))
                {
                    identity = legacyIdentity;
                }

                if (!string.IsNullOrWhiteSpace(identity))
                {
                    hiddenIdentities.Add(identity);
                }
            }
        }

        Volatile.Write(
            ref _disabledProgramIdentities,
            hiddenIdentities.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to parse the Apps settings file as a JSON object.")]
    private static partial void LogInvalidSettingsJson(MEL.ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to load Apps settings.")]
    private static partial void LogSettingsLoadFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to serialize Apps settings as a JSON object.")]
    private static partial void LogSettingsSerializationFailed(MEL.ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to save Apps settings.")]
    private static partial void LogSettingsSaveFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to remove a temporary Apps settings file.")]
    private static partial void LogTemporarySettingsFileRemovalFailed(MEL.ILogger logger, Exception exception);
}
