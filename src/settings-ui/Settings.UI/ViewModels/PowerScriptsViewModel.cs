// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerScriptsViewModel : Observable
    {
        private const string HostExeName = "PowerScripts.Host.exe";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly JsonSerializerOptions WriteJsonOptions = new() { WriteIndented = true };

        private static readonly string[] CatalogueIoTokens = { string.Empty, "none", "files", "text" };

        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly Func<string, int> _sendConfigMsg;
        private readonly Dictionary<string, MxcScriptOverrideSnapshot> _mxcScriptOverrides = new(StringComparer.OrdinalIgnoreCase);

        private bool _isEnabled;
        private string _scriptsFolder;
        private int _pythonModeIndex;
        private string _pythonInterpreterPath;
        private string _wslDistribution;
        private bool _isMxcEnabled = true;
        private bool _isMxcSupported;
        private bool _mxcUsesPlatformDefault = true;
        private string _mxcSupportMessage = GetResourceString("PowerScripts_MxcSupportChecking");
        private List<string> _mxcDisabledPolicies = new();
        private bool _mxcRiskAccepted;
        private bool _hasRejectedGlobalMxcWeakening;
        private int _mxcProbeGeneration;
        private int _scriptSortIndex;
        private int _scriptInputFilterIndex;
        private int _scriptOutputFilterIndex;
        private string _scriptSearchText = string.Empty;

        public PowerScriptsViewModel(ISettingsRepository<GeneralSettings> generalSettingsRepository, Func<string, int> sendConfigMsg)
        {
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);

            _generalSettingsRepository = generalSettingsRepository;
            _sendConfigMsg = sendConfigMsg;

            // The module-owned config.json override (if present) is authoritative, since the runner
            // strips PowerScripts from settings.json on launch; fall back to the settings flag.
            _isEnabled = ReadEnabledOverride() ?? generalSettingsRepository.SettingsConfig.Enabled.PowerScripts;
            _scriptsFolder = ResolveScriptsFolder();

            Scripts = new ObservableCollection<PowerScriptListItem>();
            WslDistributions = new ObservableCollection<string>();

            LoadMxcSettings();
            LoadPythonSettings();
            LoadWslDistributions();
        }

        public ObservableCollection<PowerScriptListItem> Scripts { get; }

        public ObservableCollection<PowerScriptListItem> VisibleScripts { get; } = new();

        public int ScriptSortIndex
        {
            get => _scriptSortIndex;
            set
            {
                if (_scriptSortIndex != value)
                {
                    _scriptSortIndex = value;
                    RefreshCatalogue();
                    OnPropertyChanged();
                }
            }
        }

        public int ScriptInputFilterIndex
        {
            get => _scriptInputFilterIndex;
            set
            {
                if (_scriptInputFilterIndex != value)
                {
                    _scriptInputFilterIndex = value;
                    RefreshCatalogue();
                    OnPropertyChanged();
                }
            }
        }

        public int ScriptOutputFilterIndex
        {
            get => _scriptOutputFilterIndex;
            set
            {
                if (_scriptOutputFilterIndex != value)
                {
                    _scriptOutputFilterIndex = value;
                    RefreshCatalogue();
                    OnPropertyChanged();
                }
            }
        }

        public string ScriptSearchText
        {
            get => _scriptSearchText;
            set
            {
                if (_scriptSearchText != value)
                {
                    _scriptSearchText = value;
                    RefreshCatalogue();
                    OnPropertyChanged();
                }
            }
        }

        public bool HasNoMatchingScripts => HasScripts && VisibleScripts.Count == 0;

        public string CatalogueCount => GetResourceString("PowerScripts_CatalogueCount")
            .Replace("{0}", VisibleScripts.Count.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace("{1}", RuntimeEnabledScriptCount.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

        /// <summary>The WSL distributions detected via <c>wsl.exe -l -q</c>, offered when Python runs in WSL mode.</summary>
        public ObservableCollection<string> WslDistributions { get; }

        public bool HasScripts => RuntimeEnabledScriptCount > 0;

        private int RuntimeEnabledScriptCount => Scripts.Count(script => IsScriptRuntimeEnabled(script, PythonModeIndex != 0));

        /// <summary>
        /// The folder PowerScripts scans for <c>.tool.json</c> descriptors. Persisted to
        /// the shared <c>config.json</c> so Settings and the Explorer context menu resolve the same folder.
        /// </summary>
        public string ScriptsFolder
        {
            get => _scriptsFolder;
            private set
            {
                if (_scriptsFolder != value)
                {
                    _scriptsFolder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomFolder));
                }
            }
        }

        public bool IsCustomFolder =>
            !string.Equals(ScriptsFolder, DefaultScriptsFolder, StringComparison.OrdinalIgnoreCase);

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;

                    GeneralSettings generalSettings = _generalSettingsRepository.SettingsConfig;
                    generalSettings.Enabled.PowerScripts = value;

                    if (_sendConfigMsg != null)
                    {
                        var outgoing = new OutGoingGeneralSettings(generalSettings);
                        _sendConfigMsg(outgoing.ToString());
                    }

                    // The shared Host uses this restart-durable gate independently of consumers.
                    SaveEnabledOverride(value);

                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Python execution mode: 0 = Disabled, 1 = Windows, 2 = existing WSL, 3 = managed Linux. Persisted to the shared
        /// <c>config.json</c>'s <c>python.mode</c> so the Host runs Python PowerScripts the same way from
        /// every surface (a hotkey, the context menu, or Advanced Paste).
        /// </summary>
        public int PythonModeIndex
        {
            get => _pythonModeIndex;
            set
            {
                if (_pythonModeIndex != value)
                {
                    _pythonModeIndex = value;
                    SavePythonSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsWindowsMode));
                    OnPropertyChanged(nameof(IsWslMode));
                    OnPropertyChanged(nameof(IsManagedLinuxMode));
                    OnPropertyChanged(nameof(PythonEnvironmentDescription));
                    RefreshScriptMxcStates();
                    RefreshCatalogue();
                }
            }
        }

        public bool IsWindowsMode => _pythonModeIndex == 1;

        public bool IsWslMode => _pythonModeIndex == 2;

        public bool IsManagedLinuxMode => _pythonModeIndex == 3;

        public string PythonEnvironmentDescription => GetResourceString(_pythonModeIndex switch
        {
            1 => "PowerScripts_EnvironmentWindowsHelp",
            2 => "PowerScripts_EnvironmentWslHelp",
            3 => "PowerScripts_EnvironmentLinuxHelp",
            _ => "PowerScripts_EnvironmentDisabledHelp",
        });

        public bool IsMxcEnabled => _isMxcEnabled;

        public bool IsMxcSupported => _isMxcSupported;

        public bool IsMxcUnsupported => !_isMxcSupported;

        public bool CanChangeMxcEnabled => _isMxcSupported || _isMxcEnabled;

        public string MxcSupportMessage => _mxcSupportMessage;

        public bool IsMxcFilesystemRestrictionEnabled => IsGlobalMxcPolicyEnabled(PowerScriptListItem.FilesystemPolicy);

        public bool IsMxcNetworkRestrictionEnabled => IsGlobalMxcPolicyEnabled(PowerScriptListItem.NetworkPolicy);

        public bool IsMxcUiRestrictionEnabled => IsGlobalMxcPolicyEnabled(PowerScriptListItem.UiPolicy);

        public bool IsMxcLeastPrivilegeRestrictionEnabled => IsGlobalMxcPolicyEnabled(PowerScriptListItem.LeastPrivilegePolicy);

        public async Task RefreshMxcPlatformSupportAsync()
        {
            var generation = Interlocked.Increment(ref _mxcProbeGeneration);
            var support = await Task.Run(LoadMxcSupportFromHost);
            if (generation != Volatile.Read(ref _mxcProbeGeneration))
            {
                return;
            }

            ApplyMxcPlatformSupport(support);
        }

        /// <summary>Optional explicit Windows interpreter path; empty means auto-detect (py.exe / python.exe).</summary>
        public string PythonInterpreterPath
        {
            get => _pythonInterpreterPath;
            set
            {
                var normalized = value ?? string.Empty;
                if (_pythonInterpreterPath != normalized)
                {
                    _pythonInterpreterPath = normalized;
                    SavePythonSettings();
                    OnPropertyChanged();
                    RefreshScriptMxcStates();
                }
            }
        }

        /// <summary>The WSL distribution scripts run in; empty means the default distribution.</summary>
        public string WslDistribution
        {
            get => _wslDistribution;
            set
            {
                var normalized = value ?? string.Empty;
                if (_wslDistribution != normalized)
                {
                    _wslDistribution = normalized;
                    SavePythonSettings();
                    OnPropertyChanged();
                    RefreshScriptMxcStates();
                }
            }
        }

        public void SetPythonInterpreterPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                PythonInterpreterPath = path.Trim();
            }
        }

        public bool TrySetMxcEnabled(bool enabled, bool isRiskAccepted)
        {
            if (_isMxcEnabled == enabled)
            {
                return true;
            }

            if (!enabled && !isRiskAccepted)
            {
                return false;
            }

            _isMxcEnabled = enabled;
            _mxcUsesPlatformDefault = false;
            UpdateGlobalMxcRiskAcceptance();
            SaveGlobalMxcSettings();
            OnPropertyChanged(nameof(IsMxcEnabled));
            RefreshScriptMxcStates();
            return true;
        }

        public bool IsGlobalMxcPolicyEnabled(string policy) =>
            !_mxcDisabledPolicies.Contains(policy, StringComparer.Ordinal);

        public bool TrySetGlobalMxcPolicy(string policy, bool isEnabled, bool isRiskAccepted)
        {
            if (!IsKnownMxcPolicy(policy) || IsGlobalMxcPolicyEnabled(policy) == isEnabled)
            {
                return IsKnownMxcPolicy(policy);
            }

            if (!isEnabled && !isRiskAccepted)
            {
                return false;
            }

            if (isEnabled)
            {
                _mxcDisabledPolicies.RemoveAll(item => string.Equals(item, policy, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _mxcDisabledPolicies.Add(CanonicalizeMxcPolicy(policy));
                _mxcDisabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(_mxcDisabledPolicies);
            }

            UpdateGlobalMxcRiskAcceptance();
            SaveGlobalMxcSettings();
            NotifyGlobalMxcPolicyChanged(policy);
            RefreshScriptMxcStates();
            return true;
        }

        public async Task<bool> TrySetScriptMxcModeAsync(PowerScriptListItem script, int modeIndex, bool isRiskAccepted)
        {
            if (script is null || string.IsNullOrWhiteSpace(script.Id) || modeIndex < 0 || modeIndex > 2)
            {
                return false;
            }

            bool? enabledOverride = modeIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };

            if (enabledOverride == false && !isRiskAccepted)
            {
                return false;
            }

            var enabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(script.MxcEnabledPolicies);
            var disabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(script.MxcDisabledPolicies);
            await SaveScriptMxcSettingsAsync(script, enabledOverride, enabledPolicies, disabledPolicies);
            return true;
        }

        public async Task<bool> TrySetScriptMxcPolicyAsync(PowerScriptListItem script, string policy, bool isEnabled, bool isRiskAccepted)
        {
            if (script is null || string.IsNullOrWhiteSpace(script.Id) || !IsKnownMxcPolicy(policy))
            {
                return false;
            }

            if (!isEnabled && !isRiskAccepted)
            {
                return false;
            }

            var enabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(script.MxcEnabledPolicies);
            var disabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(script.MxcDisabledPolicies);
            if (isEnabled)
            {
                disabledPolicies.RemoveAll(item => string.Equals(item, policy, StringComparison.OrdinalIgnoreCase));
                if (!enabledPolicies.Contains(policy, StringComparer.OrdinalIgnoreCase))
                {
                    enabledPolicies.Add(CanonicalizeMxcPolicy(policy));
                }
            }
            else if (!disabledPolicies.Contains(policy, StringComparer.OrdinalIgnoreCase))
            {
                enabledPolicies.RemoveAll(item => string.Equals(item, policy, StringComparison.OrdinalIgnoreCase));
                disabledPolicies.Add(CanonicalizeMxcPolicy(policy));
            }

            await SaveScriptMxcSettingsAsync(script, script.MxcEnabledOverride, enabledPolicies, disabledPolicies);
            return true;
        }

        public async Task<bool> TrySetScriptMxcConfigurationAsync(
            PowerScriptListItem script,
            int modeIndex,
            IEnumerable<string> enabledPolicies,
            bool usesGlobalPolicy,
            bool isRiskAccepted)
        {
            if (script is null || string.IsNullOrWhiteSpace(script.Id) || modeIndex < 0 || modeIndex > 2)
            {
                return false;
            }

            var normalizedEnabled = usesGlobalPolicy
                ? new List<string>()
                : PowerScriptListItem.NormalizeMxcPolicies(enabledPolicies);
            var disabledPolicies = new[]
            {
                PowerScriptListItem.FilesystemPolicy,
                PowerScriptListItem.NetworkPolicy,
                PowerScriptListItem.UiPolicy,
                PowerScriptListItem.LeastPrivilegePolicy,
            }.Where(policy => !normalizedEnabled.Contains(policy, StringComparer.Ordinal)).ToList();
            bool? enabledOverride = usesGlobalPolicy ? null : modeIndex switch
            {
                1 => true,
                2 => false,
                _ => null,
            };

            if (usesGlobalPolicy)
            {
                disabledPolicies.Clear();
            }

            if (RequiresMxcRiskAcceptance(enabledOverride, disabledPolicies) && !isRiskAccepted)
            {
                return false;
            }

            await SaveScriptMxcSettingsAsync(script, enabledOverride, normalizedEnabled, disabledPolicies, usesGlobalPolicy);
            return true;
        }

        public static bool RequiresMxcRiskAcceptance(bool? enabled, IEnumerable<string> disabledPolicies) =>
            enabled == false || PowerScriptListItem.NormalizeMxcPolicies(disabledPolicies).Count > 0;

        public static bool ResolveMxcEnabled(bool? explicitPreference, bool isPlatformSupported) =>
            explicitPreference ?? isPlatformSupported;

        private static bool IsScriptRuntimeEnabled(PowerScriptListItem script, bool pythonEnabled) =>
            pythonEnabled || !string.Equals(script.Runtime, "python", StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<PowerScriptListItem> FilterAndSortScripts(
            IEnumerable<PowerScriptListItem> scripts,
            int sortIndex,
            int inputFilterIndex,
            int outputFilterIndex = 0,
            string searchText = null,
            bool pythonEnabled = true)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(inputFilterIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(inputFilterIndex, CatalogueIoTokens.Length);
            ArgumentOutOfRangeException.ThrowIfNegative(outputFilterIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(outputFilterIndex, CatalogueIoTokens.Length);
            var search = searchText?.Trim();
            var filtered = scripts.Where(script =>
                IsScriptRuntimeEnabled(script, pythonEnabled) &&
                (inputFilterIndex == 0 ||
                    string.Equals(script.Io?.Input ?? "none", CatalogueIoTokens[inputFilterIndex], StringComparison.OrdinalIgnoreCase)) &&
                (outputFilterIndex == 0 ||
                    string.Equals(script.Io?.Output ?? "none", CatalogueIoTokens[outputFilterIndex], StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(search) || script.Name?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true));
            var names = StringComparer.CurrentCultureIgnoreCase;
            var ordered = sortIndex switch
            {
                0 => filtered.OrderBy(script => script.Name, names),
                1 => filtered.OrderByDescending(script => script.Name, names),
                2 => filtered.OrderBy(script => script.Io?.Input, StringComparer.OrdinalIgnoreCase).ThenBy(script => script.Name, names),
                3 => filtered.OrderBy(script => script.Io?.Output, StringComparer.OrdinalIgnoreCase).ThenBy(script => script.Name, names),
                4 => filtered.OrderBy(script => script.Runtime, StringComparer.OrdinalIgnoreCase).ThenBy(script => script.Name, names),
                _ => throw new ArgumentOutOfRangeException(nameof(sortIndex)),
            };
            return ordered.ThenBy(script => script.Id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void RefreshCatalogue(bool preserveEditorOrder = false)
        {
            var visible = FilterAndSortScripts(
                Scripts, ScriptSortIndex, ScriptInputFilterIndex, ScriptOutputFilterIndex, ScriptSearchText, PythonModeIndex != 0);
            if (preserveEditorOrder)
            {
                // Saving must not move focused editors or recycle another script's draft.
                var matching = visible.ToHashSet();
                var existing = VisibleScripts.ToHashSet();
                visible = VisibleScripts.Where(matching.Contains)
                    .Concat(visible.Where(script => !existing.Contains(script))).ToList();
            }

            var retained = visible.ToHashSet();
            for (var index = VisibleScripts.Count - 1; index >= 0; index--)
            {
                if (!retained.Contains(VisibleScripts[index]))
                {
                    VisibleScripts.RemoveAt(index);
                }
            }

            for (var index = 0; index < visible.Count; index++)
            {
                var script = visible[index];
                if (index < VisibleScripts.Count && ReferenceEquals(VisibleScripts[index], script))
                {
                    continue;
                }

                var previousIndex = VisibleScripts.IndexOf(script);
                if (previousIndex >= 0)
                {
                    VisibleScripts.Move(previousIndex, index);
                }
                else
                {
                    VisibleScripts.Insert(index, script);
                }
            }

            OnPropertyChanged(nameof(CatalogueCount));
            OnPropertyChanged(nameof(HasScripts));
            OnPropertyChanged(nameof(HasNoMatchingScripts));
        }

        /// <summary>Persists a user-chosen scripts folder and refreshes every surface that reads it.</summary>
        public void SetScriptsFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            SaveConfiguredScriptsRoot(folder.Trim());
            ScriptsFolder = ResolveScriptsFolder();
        }

        /// <summary>Clears the override so the default folder under %LOCALAPPDATA% is used again.</summary>
        public void ResetScriptsFolder()
        {
            SaveConfiguredScriptsRoot(null);
            ScriptsFolder = ResolveScriptsFolder();
        }

        private static string ModuleDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "PowerScripts");

        private static string ConfigFilePath => Path.Combine(ModuleDirectory, "config.json");

        private static string DefaultScriptsFolder => Path.Combine(ModuleDirectory, "scripts");

        private static string ResolveScriptsFolder()
        {
            var fromEnv = Environment.GetEnvironmentVariable("POWERSCRIPTS_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv;
            }

            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    using var stream = File.OpenRead(ConfigFilePath);
                    using var document = JsonDocument.Parse(stream);
                    if (document.RootElement.TryGetProperty("scriptsRoot", out var value) &&
                        value.ValueKind == JsonValueKind.String)
                    {
                        var root = value.GetString();
                        if (!string.IsNullOrWhiteSpace(root))
                        {
                            return root;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt or unreadable config falls back to the default.
            }

            return DefaultScriptsFolder;
        }

        private static void SaveConfiguredScriptsRoot(string folder)
        {
            var normalized = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            var config = LoadConfigNode();
            config["scriptsRoot"] = normalized;
            WriteConfigNode(config);
        }

        /// <summary>Reads the module <c>config.json</c> into a mutable node, preserving unknown keys.</summary>
        private static JsonObject LoadConfigNode()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var text = File.ReadAllText(ConfigFilePath);
                    if (JsonNode.Parse(text) is JsonObject existing)
                    {
                        return existing;
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt config is replaced rather than blocking the write.
            }

            return new JsonObject();
        }

        private static void WriteConfigNode(JsonObject config)
        {
            Directory.CreateDirectory(ModuleDirectory);
            File.WriteAllText(ConfigFilePath, config.ToJsonString(WriteJsonOptions));
        }

        private void LoadMxcSettings()
        {
            _isMxcSupported = false;
            _isMxcEnabled = true;
            _mxcUsesPlatformDefault = true;
            _mxcDisabledPolicies = new List<string>();
            _mxcRiskAccepted = false;
            _hasRejectedGlobalMxcWeakening = false;
            _mxcScriptOverrides.Clear();

            try
            {
                var config = LoadConfigNode();
                if (config["mxc"] is not JsonObject mxc)
                {
                    return;
                }

                var settings = mxc.Deserialize<MxcConfigurationSnapshot>(JsonOptions);
                if (settings is null)
                {
                    return;
                }

                bool? explicitEnabled = null;
                if (mxc["enabled"] is JsonValue enabledValue &&
                    enabledValue.TryGetValue<bool>(out var enabled))
                {
                    explicitEnabled = enabled;
                }

                _mxcUsesPlatformDefault = !explicitEnabled.HasValue;
                var disabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(settings.DisabledPolicies);
                var requestedEnabled = explicitEnabled ?? true;
                if (!RequiresMxcRiskAcceptance(requestedEnabled, disabledPolicies) ||
                    settings.RiskAccepted)
                {
                    _isMxcEnabled = requestedEnabled;
                    _mxcDisabledPolicies = disabledPolicies;
                    _mxcRiskAccepted = RequiresMxcRiskAcceptance(_isMxcEnabled, _mxcDisabledPolicies);
                }
                else
                {
                    _isMxcEnabled = true;
                    _mxcDisabledPolicies = new List<string>();
                    _mxcRiskAccepted = false;
                    _hasRejectedGlobalMxcWeakening = true;
                }

                if (settings.Scripts is null)
                {
                    return;
                }

                foreach (var (id, scriptOverride) in settings.Scripts)
                {
                    if (string.IsNullOrWhiteSpace(id) || scriptOverride is null)
                    {
                        continue;
                    }

                    var normalizedDisabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(scriptOverride.DisabledPolicies);
                    var normalizedEnabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(scriptOverride.EnabledPolicies);
                    normalizedDisabledPolicies.RemoveAll(policy =>
                        normalizedEnabledPolicies.Contains(policy, StringComparer.Ordinal));
                    if (RequiresMxcRiskAcceptance(scriptOverride.Enabled, normalizedDisabledPolicies) && !scriptOverride.RiskAccepted)
                    {
                        _mxcScriptOverrides[id] = new MxcScriptOverrideSnapshot
                        {
                            Enabled = true,
                            EnabledPolicies = new List<string>
                            {
                                PowerScriptListItem.FilesystemPolicy,
                                PowerScriptListItem.NetworkPolicy,
                                PowerScriptListItem.UiPolicy,
                                PowerScriptListItem.LeastPrivilegePolicy,
                            },
                        };
                    }
                    else
                    {
                        _mxcScriptOverrides[id] = new MxcScriptOverrideSnapshot
                        {
                            Enabled = scriptOverride.Enabled,
                            EnabledPolicies = normalizedEnabledPolicies,
                            DisabledPolicies = normalizedDisabledPolicies,
                            RiskAccepted = RequiresMxcRiskAcceptance(scriptOverride.Enabled, normalizedDisabledPolicies),
                        };
                    }
                }
            }
            catch (Exception)
            {
                // An invalid preference falls back to the machine-derived default.
                _isMxcEnabled = _isMxcSupported;
                _mxcUsesPlatformDefault = true;
                _mxcDisabledPolicies = new List<string>();
                _mxcRiskAccepted = false;
                _hasRejectedGlobalMxcWeakening = false;
                _mxcScriptOverrides.Clear();
            }
        }

        private void SaveGlobalMxcSettings()
        {
            var config = LoadConfigNode();
            var mxc = GetOrCreateObject(config, "mxc");
            if (_mxcUsesPlatformDefault)
            {
                mxc.Remove("enabled");
            }
            else
            {
                mxc["enabled"] = _isMxcEnabled;
            }

            mxc["disabledPolicies"] = CreateJsonArray(_mxcDisabledPolicies);
            mxc["riskAccepted"] = _mxcRiskAccepted;
            mxc["scripts"] = CreateScriptOverridesJson();
            WriteConfigNode(config);

            if (_hasRejectedGlobalMxcWeakening)
            {
                _hasRejectedGlobalMxcWeakening = false;
                RefreshScriptMxcStates();
            }
        }

        private JsonObject CreateScriptOverridesJson()
        {
            var scripts = new JsonObject();
            foreach (var (id, scriptOverride) in _mxcScriptOverrides)
            {
                scripts[id] = new JsonObject
                {
                    ["enabled"] = scriptOverride.Enabled.HasValue ? JsonValue.Create(scriptOverride.Enabled.Value) : null,
                    ["enabledPolicies"] = CreateJsonArray(scriptOverride.EnabledPolicies),
                    ["disabledPolicies"] = CreateJsonArray(scriptOverride.DisabledPolicies),
                    ["riskAccepted"] = scriptOverride.RiskAccepted,
                };
            }

            return scripts;
        }

        private async Task SaveScriptMxcSettingsAsync(
            PowerScriptListItem script,
            bool? enabledOverride,
            List<string> enabledPolicies,
            List<string> disabledPolicies,
            bool usesGlobalPolicy = false)
        {
            enabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(enabledPolicies);
            disabledPolicies = PowerScriptListItem.NormalizeMxcPolicies(disabledPolicies);
            disabledPolicies.RemoveAll(policy => enabledPolicies.Contains(policy, StringComparer.Ordinal));
            var riskAccepted = RequiresMxcRiskAcceptance(enabledOverride, disabledPolicies);
            var storedOverride = new JsonObject();
            storedOverride["enabled"] = enabledOverride.HasValue ? JsonValue.Create(enabledOverride.Value) : null;
            storedOverride["enabledPolicies"] = CreateJsonArray(enabledPolicies);
            storedOverride["disabledPolicies"] = CreateJsonArray(disabledPolicies);
            storedOverride["riskAccepted"] = riskAccepted;
            DeserializeSnapshot(await InvokeScriptSettingsHostAsync(
                "save",
                script.EntryFullPath,
                new JsonObject { ["mxc"] = usesGlobalPolicy ? null : storedOverride }));
            await ReloadScriptsAsync(preserveEditorOrder: true);
        }

        private void ApplyMxcSettingsToScript(PowerScriptListItem script)
        {
            if (script.HasLocalMxcSettings)
            {
                script.ApplyLocalMxcSettings(_isMxcEnabled, _mxcDisabledPolicies);
            }
            else if (!_hasRejectedGlobalMxcWeakening &&
                _mxcScriptOverrides.TryGetValue(script.Id, out var scriptOverride))
            {
                script.ApplyMxcSettings(
                    scriptOverride.Enabled,
                    scriptOverride.EnabledPolicies,
                    scriptOverride.DisabledPolicies,
                    _isMxcEnabled,
                    _mxcDisabledPolicies);
            }
            else
            {
                script.ApplyMxcSettings(
                    null,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    _isMxcEnabled,
                    _mxcDisabledPolicies);
            }

            ApplyExecutionSummary(script);
        }

        private void RefreshScriptMxcStates()
        {
            foreach (var script in Scripts)
            {
                ApplyMxcSettingsToScript(script);
            }
        }

        private void UpdateGlobalMxcRiskAcceptance()
        {
            var platformDefaultOff = _mxcUsesPlatformDefault &&
                                     !_isMxcSupported &&
                                     _mxcDisabledPolicies.Count == 0;
            _mxcRiskAccepted = !platformDefaultOff &&
                               RequiresMxcRiskAcceptance(_isMxcEnabled, _mxcDisabledPolicies);
        }

        private void ApplyMxcPlatformSupport(MxcSupportSnapshot support)
        {
            _isMxcSupported = support.IsSupported;
            _mxcSupportMessage = support.Reason;

            if (_mxcUsesPlatformDefault)
            {
                _isMxcEnabled = _isMxcSupported;
                UpdateGlobalMxcRiskAcceptance();
                OnPropertyChanged(nameof(IsMxcEnabled));
                RefreshScriptMxcStates();
            }

            OnPropertyChanged(nameof(IsMxcSupported));
            OnPropertyChanged(nameof(IsMxcUnsupported));
            OnPropertyChanged(nameof(CanChangeMxcEnabled));
            OnPropertyChanged(nameof(MxcSupportMessage));
        }

        private void NotifyGlobalMxcPolicyChanged(string policy)
        {
            switch (CanonicalizeMxcPolicy(policy))
            {
                case PowerScriptListItem.FilesystemPolicy:
                    OnPropertyChanged(nameof(IsMxcFilesystemRestrictionEnabled));
                    break;
                case PowerScriptListItem.NetworkPolicy:
                    OnPropertyChanged(nameof(IsMxcNetworkRestrictionEnabled));
                    break;
                case PowerScriptListItem.UiPolicy:
                    OnPropertyChanged(nameof(IsMxcUiRestrictionEnabled));
                    break;
                case PowerScriptListItem.LeastPrivilegePolicy:
                    OnPropertyChanged(nameof(IsMxcLeastPrivilegeRestrictionEnabled));
                    break;
            }
        }

        private static bool IsKnownMxcPolicy(string policy) =>
            !string.IsNullOrWhiteSpace(policy) &&
            new[]
            {
                PowerScriptListItem.FilesystemPolicy,
                PowerScriptListItem.NetworkPolicy,
                PowerScriptListItem.UiPolicy,
                PowerScriptListItem.LeastPrivilegePolicy,
            }.Contains(policy, StringComparer.OrdinalIgnoreCase);

        private static string CanonicalizeMxcPolicy(string policy) =>
            new[]
            {
                PowerScriptListItem.FilesystemPolicy,
                PowerScriptListItem.NetworkPolicy,
                PowerScriptListItem.UiPolicy,
                PowerScriptListItem.LeastPrivilegePolicy,
            }.First(item => string.Equals(item, policy, StringComparison.OrdinalIgnoreCase));

        private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
        {
            if (parent[propertyName] is JsonObject existing)
            {
                return existing;
            }

            var created = new JsonObject();
            parent[propertyName] = created;
            return created;
        }

        private static JsonArray CreateJsonArray(IEnumerable<string> values)
        {
            var array = new JsonArray();
            foreach (var value in values)
            {
                array.Add(value);
            }

            return array;
        }

        /// <summary>Reads the module-owned enabled override from config.json, or null when absent.</summary>
        private static bool? ReadEnabledOverride()
        {
            try
            {
                var config = LoadConfigNode();
                if (config["enabled"] is JsonValue value && value.TryGetValue<bool>(out var enabled))
                {
                    return enabled;
                }
            }
            catch (Exception)
            {
                // A corrupt/unreadable config yields no override.
            }

            return null;
        }

        /// <summary>Persists the enabled override into config.json, preserving all other keys.</summary>
        private static void SaveEnabledOverride(bool enabled)
        {
            var config = LoadConfigNode();
            config["enabled"] = enabled;
            WriteConfigNode(config);
        }

        private void LoadPythonSettings()
        {
            _pythonModeIndex = 0;
            _pythonInterpreterPath = string.Empty;
            _wslDistribution = string.Empty;

            try
            {
                var config = LoadConfigNode();
                if (config["python"] is JsonObject python)
                {
                    var mode = python["mode"]?.GetValue<string>() ?? "disabled";
                    _pythonModeIndex = mode.ToLowerInvariant() switch
                    {
                        "windows" => 1,
                        "wsl" => 2,
                        "managedlinux" => 3,
                        _ => 0,
                    };

                    _pythonInterpreterPath = python["windows"]?["interpreterPath"]?.GetValue<string>() ?? string.Empty;
                    _wslDistribution = python["wsl"]?["distribution"]?.GetValue<string>() ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // Missing/corrupt config leaves Python disabled with default paths.
            }
        }

        /// <summary>
        /// Persists the Python section into <c>config.json</c>, preserving <c>scriptsRoot</c> and any
        /// timeout the Host may have written. Existing environment settings are retained when switching.
        /// </summary>
        private void SavePythonSettings()
        {
            var config = LoadConfigNode();

            var mode = _pythonModeIndex switch
            {
                1 => "windows",
                2 => "wsl",
                3 => "managedLinux",
                _ => "disabled",
            };

            var existingTimeout = (config["python"] as JsonObject)?["timeoutSeconds"]?.GetValue<int>() ?? 30;

            var python = config["python"] as JsonObject;
            if (python is null)
            {
                python = new JsonObject();
                config["python"] = python;
            }

            python["mode"] = mode;
            python["windows"] = new JsonObject { ["interpreterPath"] = _pythonInterpreterPath ?? string.Empty };
            python["wsl"] = new JsonObject { ["distribution"] = _wslDistribution ?? string.Empty };
            python["timeoutSeconds"] = existingTimeout;

            WriteConfigNode(config);
        }

        /// <summary>Populates <see cref="WslDistributions"/> from <c>wsl.exe -l -q</c> (best-effort).</summary>
        private void LoadWslDistributions()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "-l -q",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,

                    // wsl.exe emits UTF-16LE for list output.
                    StandardOutputEncoding = Encoding.Unicode,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                foreach (var line in output.Split('\n'))
                {
                    var distro = line.Trim().Trim('\0', '\r');
                    if (!string.IsNullOrWhiteSpace(distro) && !WslDistributions.Contains(distro))
                    {
                        WslDistributions.Add(distro);
                    }
                }
            }
            catch (Exception)
            {
                // WSL not installed / unavailable: leave the list empty.
            }
        }

        private static string ResolveHostPath()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, HostExeName),
                Path.Combine(AppContext.BaseDirectory, "PowerScripts", HostExeName),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerScripts", HostExeName)),
                Path.Combine(ModuleDirectory, HostExeName),
            };

            // Prototype dev fallback: when running an in-repo build, the Host isn't copied next to
            // Settings, so walk up from the base directory and probe the Host project's bin output.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                foreach (var config in new[] { "Debug", "Release" })
                {
                    var hostBin = Path.Combine(
                        dir.FullName,
                        "src",
                        "modules",
                        "PowerScripts",
                        "PowerScripts.Host",
                        "bin",
                        config);

                    if (Directory.Exists(hostBin))
                    {
                        var found = Directory
                            .EnumerateFiles(hostBin, HostExeName, SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(found))
                        {
                            candidates.Add(found);
                        }
                    }
                }

                dir = dir.Parent;
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        internal static IReadOnlyList<PowerScriptListItem> LoadScriptsFromHost()
        {
            string hostPath = ResolveHostPath();
            if (string.IsNullOrEmpty(hostPath))
            {
                return Array.Empty<PowerScriptListItem>();
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = "list --json --include-disabled",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return Array.Empty<PowerScriptListItem>();
                }

                string json = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                return JsonSerializer.Deserialize<List<PowerScriptListItem>>(json, JsonOptions)
                    ?? new List<PowerScriptListItem>();
            }
            catch (Exception)
            {
                // Prototype: a missing/failed host simply yields an empty list.
                return Array.Empty<PowerScriptListItem>();
            }
        }

        private static MxcSupportSnapshot LoadMxcSupportFromHost()
        {
            string hostPath = ResolveHostPath();
            if (string.IsNullOrEmpty(hostPath))
            {
                return new MxcSupportSnapshot
                {
                    Reason = GetResourceString("PowerScripts_MxcSupportHostMissing"),
                };
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = "mxc-support --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return new MxcSupportSnapshot { Reason = GetResourceString("PowerScripts_MxcSupportProbeStartFailed") };
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }

                    return new MxcSupportSnapshot { Reason = GetResourceString("PowerScripts_MxcSupportProbeFailed") };
                }

                string json = outputTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    return new MxcSupportSnapshot { Reason = GetResourceString("PowerScripts_MxcSupportProbeFailed") };
                }

                return JsonSerializer.Deserialize<MxcSupportSnapshot>(json, JsonOptions)
                    ?? new MxcSupportSnapshot { Reason = GetResourceString("PowerScripts_MxcSupportProbeNoResult") };
            }
            catch (Exception)
            {
                return new MxcSupportSnapshot { Reason = GetResourceString("PowerScripts_MxcSupportUnknown") };
            }
        }

        private static string GetResourceString(string resourceName) =>
            ResourceLoaderInstance.ResourceLoader.GetString(resourceName);

        private sealed class MxcSupportSnapshot
        {
            public bool IsSupported { get; set; }

            public string Reason { get; set; } = string.Empty;
        }

        private sealed class MxcConfigurationSnapshot
        {
            public bool Enabled { get; set; } = true;

            public string ExecutorPath { get; set; } = string.Empty;

            public List<string> DisabledPolicies { get; set; } = new();

            public bool RiskAccepted { get; set; }

            public Dictionary<string, MxcScriptOverrideSnapshot> Scripts { get; set; } =
                new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class MxcScriptOverrideSnapshot
        {
            public bool? Enabled { get; set; }

            public List<string> EnabledPolicies { get; set; } = new();

            public List<string> DisabledPolicies { get; set; } = new();

            public bool RiskAccepted { get; set; }
        }
    }
}
