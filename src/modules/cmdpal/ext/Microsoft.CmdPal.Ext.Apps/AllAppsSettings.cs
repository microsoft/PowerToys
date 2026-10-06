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
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps;

public partial class AllAppsSettings : JsonSettingsManager
{
    private const int DefaultSearchResultLimit = 10;
    private const ExecutableNameMatchMode DefaultExecutableNameMatchMode = ExecutableNameMatchMode.FilenameOnly;
    private const int AppCommandAliasesReadAttempts = 3;
    private const int AppCommandAliasesReadRetryDelayMs = 25;
    private const string DisabledProgramSourcesPropertyName = "DisabledProgramSources";
    private const string DisabledProgramUniqueIdentifierPropertyName = "UniqueIdentifier";
    private const string AppCommandAliasesPropertyName = "AppCommandAliases";

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
    private readonly Lock _settingsFileLock = new();
    private readonly MEL.ILogger<AllAppsSettings> _logger;
    private FrozenSet<string> _disabledProgramIdentities = FrozenSet<string>.Empty;
    private FrozenDictionary<string, string> _appCommandAliases = FrozenDictionary<string, string>.Empty;
    private FrozenDictionary<string, string> _savedAppCommandAliases = FrozenDictionary<string, string>.Empty;
    private FrozenSet<string> _savedHiddenIdentities = FrozenSet<string>.Empty;
    private Task? _aliasSaveTask;
    private bool _aliasSavePending;
    private bool _canSaveAppCommandAliases = true;
    private bool _hasSavedAppCommandAliases;
    private bool _legacyAppCommandAliasesPending;

    internal event EventHandler? HiddenAppsChanged;

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

    internal const char SuffixSeparator = ';';

    internal string AppCommandAliasesFilePath => AppCommandAliasesPath(FilePath);

    /// <summary>Gets when exact executable names receive priority in All Apps and Home search, resolving the default policy.</summary>
    public ExecutableNameMatchMode ExecutableNameMatchMode => _executableNameMatchMode.Value switch
    {
        "default" => DefaultExecutableNameMatchMode,
        "disabled" => ExecutableNameMatchMode.Disabled,
        "filenameOnly" => ExecutableNameMatchMode.FilenameOnly,
        "filenameAndStem" => ExecutableNameMatchMode.FilenameAndStem,
        _ => DefaultExecutableNameMatchMode,
    };

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
        Settings.Add(_executableNameMatchMode);
        Settings.Add(_hideAppDescriptions);
        Settings.Add(_hideUninstallers);
        Settings.Add(_excludedAppNames);
        Settings.Add(_excludedAppPaths);
        Settings.Add(_enableCatalogDiagnostics);

        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    internal static string AppCommandAliasesPath(string settingsPath)
    {
        var name = Path.GetFileNameWithoutExtension(settingsPath);
        const string settingsSuffix = ".settings";
        if (name.EndsWith(settingsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^settingsSuffix.Length];
        }

        return Path.Combine(Path.GetDirectoryName(settingsPath) ?? string.Empty, $"{name}.aliases.json");
    }

    public override void LoadSettings()
    {
        WaitForAliasSavesAsync().GetAwaiter().GetResult();
        lock (_settingsFileLock)
        {
            LoadSettingsUnderFileLock();
        }
    }

    private void LoadSettingsUnderFileLock()
    {
        lock (_settingsWriterLock)
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(LoadSettings)}");
            }

            JsonObject? savedSettings = null;
            string? content = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    content = File.ReadAllText(FilePath);
                    savedSettings = JsonNode.Parse(content) as JsonObject;
                    if (savedSettings is null)
                    {
                        LogInvalidSettingsJson(_logger);
                    }
                    else
                    {
                        LoadDisabledProgramSources(savedSettings);
                        _savedHiddenIdentities = _disabledProgramIdentities;
                    }
                }
            }
            catch (Exception ex)
            {
                LogSettingsLoadFailed(_logger, ex);
            }

            LoadAppCommandAliases(savedSettings);
            if (savedSettings is not null)
            {
                try
                {
                    Settings.Update(content!);
                }
                catch (Exception ex)
                {
                    LogSettingsLoadFailed(_logger, ex);
                }
            }

            if (_legacyAppCommandAliasesPending && _canSaveAppCommandAliases)
            {
                _aliasSavePending = true;
                _aliasSaveTask ??= Task.Run(SavePendingAliases);
            }
        }
    }

    public override void SaveSettings()
    {
        lock (_settingsFileLock)
        {
            SaveSettingsUnderFileLock();
        }
    }

    internal Task WaitForAliasSavesAsync()
    {
        lock (_settingsWriterLock)
        {
            return _aliasSaveTask ?? Task.CompletedTask;
        }
    }

    /// <summary>Determines whether a canonical application identity is hidden.</summary>
    internal bool IsAppHidden(string identity) =>
        Volatile.Read(ref _disabledProgramIdentities).Contains(identity);

    /// <summary>Updates the in-memory visibility preference for a canonical application identity.</summary>
    internal bool SetAppHidden(string identity, bool hidden)
        => SetAppHidden([identity], hidden);

    internal bool SetAppHidden(IEnumerable<string> identities, bool hidden)
    {
        lock (_settingsWriterLock)
        {
            var current = _disabledProgramIdentities;
            var updated = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            if (hidden)
            {
                updated.UnionWith(identities);
            }
            else
            {
                var commandIds = identities.Select(Catalog.AppIdentity.ForCommand).ToHashSet(StringComparer.Ordinal);
                updated.RemoveWhere(identity => commandIds.Contains(Catalog.AppIdentity.ForCommand(identity))
                    || (_appCommandAliases.TryGetValue(Catalog.AppIdentity.ForCommand(identity), out var target) && commandIds.Contains(target)));
            }

            if (updated.SetEquals(current))
            {
                return false;
            }

            Volatile.Write(ref _disabledProgramIdentities, updated.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
            return true;
        }
    }

    /// <summary>Retains saved command IDs independently of localized discovery-cache snapshots.</summary>
    internal IReadOnlyDictionary<string, string> RetainAppCommandAliases(IEnumerable<AppItem> apps)
    {
        // Alias keys preserve persisted spelling; their typed command targets compare case-insensitively.
        var observed = new Dictionary<string, string>(StringComparer.Ordinal);
        var launchAliases = new HashSet<string>(StringComparer.Ordinal);
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var app in apps)
        {
            if (string.IsNullOrEmpty(app.CatalogId))
            {
                continue;
            }

            var commandId = Catalog.AppIdentity.ForCommand(app.CatalogId);
            identities[commandId] = app.CatalogId;
            var legacyId = AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget);
            if (!string.IsNullOrEmpty(app.LaunchTarget))
            {
                launchAliases.Add(legacyId);
            }

            foreach (var alias in app.CommandIds.Prepend(legacyId).Append(commandId))
            {
                // An ambiguous legacy ID cannot safely select either launch entry.
                observed[alias] = observed.TryGetValue(alias, out var previous) && !string.Equals(previous, commandId, StringComparison.OrdinalIgnoreCase) ? string.Empty : commandId;
            }
        }

        var hiddenChanged = false;
        IReadOnlyDictionary<string, string> result;
        lock (_settingsWriterLock)
        {
            var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var alias in observed)
            {
                if ((Catalog.AppIdentity.IsCommandId(alias.Key) || launchAliases.Contains(alias.Key))
                    && !string.IsNullOrEmpty(alias.Value)
                    && _appCommandAliases.TryGetValue(alias.Key, out var previous)
                    && !string.IsNullOrEmpty(previous) && !string.Equals(previous, alias.Value, StringComparison.OrdinalIgnoreCase)
                    && (!observed.TryGetValue(previous, out var target) || !string.Equals(target, previous, StringComparison.OrdinalIgnoreCase)))
                {
                    // An unchanged launch entry can follow an install move, but never replace a current app.
                    redirects[previous] = redirects.TryGetValue(previous, out var other) && !string.Equals(other, alias.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : alias.Value;
                }
            }

            foreach (var redirect in redirects)
            {
                observed[redirect.Key] = observed.TryGetValue(redirect.Key, out var target) && !string.Equals(target, redirect.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : redirect.Value;
            }

            Dictionary<string, string>? updated = null;
            foreach (var alias in _appCommandAliases)
            {
                if (observed.TryGetValue(alias.Value, out var commandId) && !string.Equals(commandId, alias.Value, StringComparison.OrdinalIgnoreCase))
                {
                    updated ??= new(_appCommandAliases, StringComparer.Ordinal);
                    updated[alias.Key] = commandId;
                }
            }

            foreach (var alias in observed)
            {
                if (alias.Key == alias.Value)
                {
                    continue;
                }

                var current = updated is null ? (IReadOnlyDictionary<string, string>)_appCommandAliases : updated;
                var exists = current.TryGetValue(alias.Key, out var previous);
                var commandId = exists && !string.Equals(previous, alias.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : alias.Value;
                if (!exists || previous != commandId)
                {
                    updated ??= new(_appCommandAliases, StringComparer.Ordinal);
                    updated[alias.Key] = commandId;
                }
            }

            if (updated is not null)
            {
                _appCommandAliases = updated.Where(pair => pair.Key != pair.Value).ToFrozenDictionary(StringComparer.Ordinal);
            }

            // Preserve path-only hides written by earlier builds when a proven identity move is observed.
            foreach (var identity in _disabledProgramIdentities)
            {
                if (_appCommandAliases.TryGetValue(Catalog.AppIdentity.ForCommand(identity), out var target)
                    && identities.TryGetValue(target, out var currentIdentity)
                    && !_disabledProgramIdentities.Contains(currentIdentity))
                {
                    hiddenChanged |= SetAppHidden(currentIdentity, hidden: true);
                }
            }

            if (updated is not null || hiddenChanged)
            {
                _aliasSavePending = true;
                _aliasSaveTask ??= Task.Run(SavePendingAliases);
            }

            result = _appCommandAliases;
        }

        if (hiddenChanged)
        {
            HiddenAppsChanged?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    private void SavePendingAliases()
    {
        while (true)
        {
            lock (_settingsWriterLock)
            {
                if (!_aliasSavePending)
                {
                    _aliasSaveTask = null;
                    return;
                }

                _aliasSavePending = false;
            }

            lock (_settingsFileLock)
            {
                if (!ReferenceEquals(Volatile.Read(ref _disabledProgramIdentities), _savedHiddenIdentities))
                {
                    SaveSettingsUnderFileLock();
                }
                else
                {
                    SaveAppCommandAliasesUnderFileLock();
                    if (_legacyAppCommandAliasesPending && _hasSavedAppCommandAliases && _canSaveAppCommandAliases)
                    {
                        RemoveLegacyAppCommandAliasesUnderFileLock();
                    }
                }
            }
        }
    }

    private void SaveSettingsUnderFileLock()
    {
        SaveAppCommandAliasesUnderFileLock();

        try
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException($"You must set a valid {nameof(FilePath)} before calling {nameof(SaveSettings)}");
            }

            JsonObject? currentSettings;
            FrozenSet<string> hiddenIdentities;
            lock (_settingsWriterLock)
            {
                currentSettings = JsonNode.Parse(Settings.ToJson()) as JsonObject;
                hiddenIdentities = _disabledProgramIdentities;
            }

            if (currentSettings is null)
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
            foreach (var hiddenIdentity in hiddenIdentities.Order(StringComparer.OrdinalIgnoreCase))
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

            if (_hasSavedAppCommandAliases && _canSaveAppCommandAliases)
            {
                savedSettings.Remove(AppCommandAliasesPropertyName);
            }

            // Remove the grouped alias format written by earlier pre-release builds.
            savedSettings.Remove("AppCommandAliasGroups");

            WriteJsonAtomically(FilePath, savedSettings);
            _savedHiddenIdentities = hiddenIdentities;
            _legacyAppCommandAliasesPending = savedSettings.ContainsKey(AppCommandAliasesPropertyName);
        }
        catch (Exception ex)
        {
            LogSettingsSaveFailed(_logger, ex);
        }
    }

    private void SaveAppCommandAliasesUnderFileLock()
    {
        var aliases = Volatile.Read(ref _appCommandAliases);
        if (string.IsNullOrEmpty(FilePath) || !_canSaveAppCommandAliases
            || (ReferenceEquals(aliases, _savedAppCommandAliases) && (!_legacyAppCommandAliasesPending || _hasSavedAppCommandAliases)))
        {
            return;
        }

        try
        {
            var commandAliases = new JsonObject();
            foreach (var alias in aliases.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                commandAliases[alias.Key] = alias.Value;
            }

            WriteJsonAtomically(AppCommandAliasesFilePath, new JsonObject { [AppCommandAliasesPropertyName] = commandAliases });
            _savedAppCommandAliases = aliases;
            _hasSavedAppCommandAliases = true;
        }
        catch (Exception ex)
        {
            LogAppCommandAliasesSaveFailed(_logger, ex);
        }
    }

    private void RemoveLegacyAppCommandAliasesUnderFileLock()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject savedSettings)
                {
                    LogInvalidSettingsJson(_logger);
                    return;
                }

                var changed = savedSettings.Remove(AppCommandAliasesPropertyName);
                changed |= savedSettings.Remove("AppCommandAliasGroups");
                if (changed)
                {
                    WriteJsonAtomically(FilePath, savedSettings);
                }
            }

            _legacyAppCommandAliasesPending = false;
        }
        catch (Exception ex)
        {
            LogSettingsSaveFailed(_logger, ex);
        }
    }

    private void WriteJsonAtomically(string path, JsonObject content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, content.ToJsonString(_serializerOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
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

    private void LoadAppCommandAliases(JsonObject? savedSettings)
    {
        _legacyAppCommandAliasesPending = savedSettings?.ContainsKey(AppCommandAliasesPropertyName) == true
            || savedSettings?.ContainsKey("AppCommandAliasGroups") == true;
        _canSaveAppCommandAliases = true;
        _hasSavedAppCommandAliases = false;
        JsonObject? commandAliases = null;
        try
        {
            var content = ReadAppCommandAliasesFile();

            // Once present, the separate file is authoritative, including an empty alias map.
            if (content is not null)
            {
                if (JsonNode.Parse(content) is not JsonObject aliasFile
                    || aliasFile[AppCommandAliasesPropertyName] is not JsonObject savedAliases
                    || savedAliases.Any(alias => string.IsNullOrWhiteSpace(alias.Key)
                        || alias.Value is not JsonValue value || !value.TryGetValue<string>(out var target) || target is null))
                {
                    throw new JsonException("The Apps command alias file must contain a string-to-string AppCommandAliases object.");
                }

                commandAliases = savedAliases;
                _hasSavedAppCommandAliases = true;
            }
            else
            {
                commandAliases = savedSettings?[AppCommandAliasesPropertyName] as JsonObject;
            }
        }
        catch (Exception ex)
        {
            // Do not overwrite unreadable alias history or revive stale settings-file aliases.
            _canSaveAppCommandAliases = false;
            LogAppCommandAliasesLoadFailed(_logger, ex);
            return;
        }

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        if (commandAliases is not null)
        {
            foreach (var alias in commandAliases)
            {
                if (!string.IsNullOrWhiteSpace(alias.Key)
                    && alias.Value is JsonValue value
                    && value.TryGetValue<string>(out var commandId)
                    && commandId is not null)
                {
                    aliases[alias.Key] = commandId;
                }
            }
        }

        _appCommandAliases = aliases.Where(pair => pair.Key != pair.Value).ToFrozenDictionary(StringComparer.Ordinal);
        _savedAppCommandAliases = _appCommandAliases;
    }

    private string? ReadAppCommandAliasesFile()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(AppCommandAliasesFilePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // An absent file can be populated from the legacy settings map.
                return null;
            }
            catch (IOException ex) when (attempt < AppCommandAliasesReadAttempts)
            {
                LogAppCommandAliasesReadRetry(_logger, ex);
                Thread.Sleep(AppCommandAliasesReadRetryDelayMs);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to parse the Apps settings file as a JSON object.")]
    private static partial void LogInvalidSettingsJson(MEL.ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to load Apps settings.")]
    private static partial void LogSettingsLoadFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to serialize Apps settings as a JSON object.")]
    private static partial void LogSettingsSerializationFailed(MEL.ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to save Apps settings.")]
    private static partial void LogSettingsSaveFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to remove a temporary Apps data file.")]
    private static partial void LogTemporarySettingsFileRemovalFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Failed to load Apps command aliases.")]
    private static partial void LogAppCommandAliasesLoadFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Failed to save Apps command aliases.")]
    private static partial void LogAppCommandAliasesSaveFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Retrying Apps command alias read after an I/O failure.")]
    private static partial void LogAppCommandAliasesReadRetry(MEL.ILogger logger, Exception exception);
}
