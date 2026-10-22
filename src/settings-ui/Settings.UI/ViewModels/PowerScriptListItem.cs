// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A single PowerScript shown in Settings, projected from effective Host metadata.
    /// </summary>
    public sealed class PowerScriptListItem : Observable
    {
        public const string FilesystemPolicy = "filesystem";
        public const string NetworkPolicy = "network";
        public const string UiPolicy = "ui";
        public const string LeastPrivilegePolicy = "leastPrivilege";

        private static readonly string[] AllMxcPolicies = { FilesystemPolicy, NetworkPolicy, UiPolicy, LeastPrivilegePolicy };

        private static readonly string[] MetadataProperties =
        {
            nameof(Id),
            nameof(Name),
            nameof(Description),
            nameof(Consumers),
            nameof(HasDescriptor),
            nameof(HasLocalSettings),
            nameof(HasLocalMxcSettings),
            nameof(LocalMxc),
            nameof(IsMxcReviewRequired),
            nameof(ConfigurationErrors),
            nameof(HasConfigurationErrors),
            nameof(ConfigurationErrorDisplay),
            nameof(ConsumersDisplay),
            nameof(ConfigureAutomationId),
            nameof(ResetAutomationId),
            nameof(ForgetAutomationId),
            nameof(InlineSettingsAutomationId),
            nameof(Runtime),
            nameof(DefaultIconGlyph),
            nameof(IconPath),
            nameof(IconWarning),
            nameof(HasIcon),
            nameof(HasIconWarning),
            nameof(IconSourcePath),
            nameof(Io),
            nameof(Input),
            nameof(Capabilities),
            nameof(Mxc),
            nameof(Trusted),
            nameof(FolderPath),
            nameof(EntryFullPath),
            nameof(Parameters),
            nameof(HasParameters),
            nameof(HasFileInput),
            nameof(ExtensionsDisplay),
            nameof(IoDisplay),
            nameof(InputDisplay),
            nameof(CatalogueSummary),
            nameof(CardAutomationId),
            nameof(CapabilitiesDisplay),
            nameof(RuntimeDisplay),
            nameof(TrustDisplay),
            nameof(HasFolderPath),
            nameof(RecommendedPoliciesDisplay),
            nameof(RecommendationSummary),
            nameof(MxcPolicyEditorAutomationId),
            nameof(OpenFolderAutomationId),
        };

        private bool? _mxcEnabledOverride;
        private List<string> _mxcEnabledPolicies = new();
        private List<string> _mxcDisabledPolicies = new();
        private List<string> _globalMxcDisabledPolicies = new();
        private bool _isMxcEffectivelyEnabled = true;
        private List<string> _effectiveDisabledPolicies = new();
        private bool _isFilesystemRestrictionEffective = true;
        private bool _isNetworkRestrictionEffective = true;
        private bool _isUiRestrictionEffective = true;
        private bool _isLeastPrivilegeRestrictionEffective = true;
        private string _mxcEffectiveDisplay = GetResourceString("PowerScripts_MxcEffectiveAll");
        private bool _iconLoadFailed;
        private int _settingsRevision;

        [JsonIgnore]
        public int SettingsRevision => _settingsRevision;

        public void InvalidateSettings()
        {
            _settingsRevision++;
            OnPropertyChanged(nameof(SettingsRevision));
        }

        public void UpdateMetadata(PowerScriptListItem script)
        {
            ArgumentNullException.ThrowIfNull(script);
            if (!string.Equals(IconPath, script.IconPath, StringComparison.OrdinalIgnoreCase))
            {
                _iconLoadFailed = false;
            }

            Id = script.Id;
            Name = script.Name;
            Description = script.Description;
            Consumers = script.Consumers;
            HasDescriptor = script.HasDescriptor;
            HasLocalSettings = script.HasLocalSettings;
            HasLocalMxcSettings = script.HasLocalMxcSettings;
            LocalMxc = script.LocalMxc;
            ConfigurationErrors = script.ConfigurationErrors;
            Runtime = script.Runtime;
            IconPath = script.IconPath;
            IconWarning = script.IconWarning;
            Io = script.Io;
            Input = script.Input;
            Capabilities = script.Capabilities;
            Mxc = script.Mxc;
            Trusted = script.Trusted;
            FolderPath = script.FolderPath;
            EntryFullPath = script.EntryFullPath;
            Parameters = script.Parameters;

            // An all-properties notification also rebinds whole-item DataContexts in the
            // repeater, recreating expanded controls and discarding their visual state.
            foreach (var property in MetadataProperties)
            {
                OnPropertyChanged(property);
            }
        }

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public List<string> Consumers { get; set; } = new();

        public bool HasDescriptor { get; set; }

        public bool HasLocalSettings { get; set; }

        public bool HasLocalMxcSettings { get; set; }

        public PowerScriptLocalMxcSettings LocalMxc { get; set; }

        public bool IsMxcReviewRequired => HasLocalMxcSettings && LocalMxc is not null &&
            !LocalMxc.RiskAccepted && (LocalMxc.Enabled == false || NormalizeMxcPolicies(LocalMxc.DisabledPolicies).Count > 0);

        public List<string> ConfigurationErrors { get; set; } = new();

        public bool HasConfigurationErrors => ConfigurationErrors is { Count: > 0 };

        public string ConfigurationErrorDisplay => string.Join(Environment.NewLine, ConfigurationErrors ?? new());

        public string ConsumersDisplay => Consumers is { Count: > 0 }
            ? string.Join(", ", Consumers)
            : GetResourceString("PowerScripts_SettingsAllConsumers");

        private string SettingsAutomationKey => $"PowerScripts_{Id}_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(EntryFullPath.ToUpperInvariant())))[..12]}";

        public string ConfigureAutomationId => $"{SettingsAutomationKey}_Configure";

        public string ResetAutomationId => $"{SettingsAutomationKey}_Reset";

        public string ForgetAutomationId => $"{SettingsAutomationKey}_Forget";

        public string InlineSettingsAutomationId => $"{SettingsAutomationKey}_Inline";

        public string Runtime { get; set; } = string.Empty;

        public string DefaultIconGlyph => string.Equals(Runtime, "Python", StringComparison.OrdinalIgnoreCase)
            ? "\uE943"
            : "\uE756";

        public string IconPath { get; set; }

        public string IconWarning { get; set; }

        public bool HasIcon => !_iconLoadFailed && !string.IsNullOrEmpty(IconPath);

        public bool HasIconWarning => !string.IsNullOrEmpty(IconWarning);

        public string IconSourcePath => HasIcon ? new Uri(IconPath).AbsoluteUri : null;

        public PowerScriptIo Io { get; set; } = new();

        public PowerScriptInput Input { get; set; }

        public List<string> Capabilities { get; set; } = new();

        public MxcMetadata Mxc { get; set; } = new();

        /// <summary>
        /// True once the user has approved this script's current content to run (trust-on-first-use).
        /// Emitted by the Host as <c>trusted</c>; recomputed from the script's content hash, so it
        /// flips back to false if the script body, descriptor, or declared capabilities change.
        /// </summary>
        public bool Trusted { get; set; }

        /// <summary>
        /// Absolute path to the folder containing this script. Surfaced with an
        /// "open folder" button so users can quickly locate a script on disk (e.g. to edit or inspect it).
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>Absolute path to the script file itself.</summary>
        public string EntryFullPath { get; set; } = string.Empty;

        /// <summary>
        /// The parameters this script declares in its manifest. Surfaced under each script so users
        /// know what a community-authored script will ask for before running it.
        /// </summary>
        public List<PowerScriptParameterItem> Parameters { get; set; } = new();

        /// <summary>True when the script declares at least one parameter.</summary>
        public bool HasParameters => Parameters is { Count: > 0 };

        public bool HasFileInput => string.Equals(Io?.Input, "files", StringComparison.OrdinalIgnoreCase);

        /// <summary>Comma-separated trigger extensions declared in the manifest (file scripts only).</summary>
        public string ExtensionsDisplay => Input?.Extensions is { Count: > 0 } exts
            ? string.Join(", ", exts)
            : "—";

        /// <summary>Resolved input/output contract used by consumers to discover compatible scripts.</summary>
        public string IoDisplay => $"{DisplayIoValue(Io?.Input)} \u2192 {DisplayIoValue(Io?.Output)}";

        public string InputDisplay => !HasFileInput || Input?.Extensions is not { Count: > 0 } extensions
            ? DisplayIoValue(Io?.Input)
            : extensions.Contains("*", StringComparer.Ordinal)
                ? GetResourceString("PowerScripts_IoAllFiles")
                : string.Join(", ", extensions
                    .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

        public string CatalogueSummary => $"{RuntimeDisplay} \u00b7 {InputDisplay} \u2192 {DisplayIoValue(Io?.Output)}";

        public string CardAutomationId => $"PowerScripts_{Id}_Card";

        /// <summary>Comma-separated list of the capabilities the script declares.</summary>
        public string CapabilitiesDisplay => Capabilities is { Count: > 0 }
            ? string.Join(", ", Capabilities)
            : "—";

        /// <summary>Friendly runtime label (e.g. "PowerShell").</summary>
        public string RuntimeDisplay => string.IsNullOrEmpty(Runtime) ? "—" : Runtime;

        private static string DisplayIoValue(string value) => value?.ToLowerInvariant() switch
        {
            null or "" or "none" => GetResourceString("PowerScripts_IoNone"),
            "files" => GetResourceString("PowerScripts_IoFiles"),
            "text" => GetResourceString("PowerScripts_IoText"),
            _ => value,
        };

        public void MarkIconUnavailable()
        {
            _iconLoadFailed = true;
            IconWarning = GetResourceString("PowerScripts_IconLoadFailed");
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(IconSourcePath));
            OnPropertyChanged(nameof(IconWarning));
            OnPropertyChanged(nameof(HasIconWarning));
        }

        /// <summary>Human-readable trust state shown in the Settings list.</summary>
        public string TrustDisplay => Trusted
            ? "Trusted"
            : "Not yet trusted — you'll be asked to allow it the first time it runs";

        /// <summary>True when a folder path is known, so the location card and open button can show.</summary>
        public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

        public bool? MxcEnabledOverride => _mxcEnabledOverride;

        public IReadOnlyList<string> MxcDisabledPolicies => _mxcDisabledPolicies;

        public IReadOnlyList<string> MxcEnabledPolicies => _mxcEnabledPolicies;

        public int MxcModeIndex => _mxcEnabledOverride switch
        {
            true => 1,
            false => 2,
            null => 0,
        };

        public bool IsMxcEffectivelyEnabled => _isMxcEffectivelyEnabled;

        public bool CanConfigureMxcPolicies => _isMxcEffectivelyEnabled;

        public bool IsFilesystemPolicyOverrideEnabled => IsFilesystemRestrictionEffective;

        public bool IsNetworkPolicyOverrideEnabled => IsNetworkRestrictionEffective;

        public bool IsUiPolicyOverrideEnabled => IsUiRestrictionEffective;

        public bool IsLeastPrivilegePolicyOverrideEnabled => IsLeastPrivilegeRestrictionEffective;

        public bool IsFilesystemRestrictionEffective => _isFilesystemRestrictionEffective;

        public bool IsNetworkRestrictionEffective => _isNetworkRestrictionEffective;

        public bool IsUiRestrictionEffective => _isUiRestrictionEffective;

        public bool IsLeastPrivilegeRestrictionEffective => _isLeastPrivilegeRestrictionEffective;

        public string MxcEffectiveDisplay => _mxcEffectiveDisplay;

        public string RecommendedPoliciesDisplay
        {
            get
            {
                var policies = NormalizeMxcPolicies(Mxc?.RecommendedPolicies);
                return policies.Count == 0
                    ? GetResourceString("PowerScripts_MxcNoRestrictions")
                    : string.Join(", ", policies.Select(GetMxcPolicyDisplayName));
            }
        }

        public string RecommendationSummary
        {
            get
            {
                var recommended = NormalizeMxcPolicies(Mxc?.RecommendedPolicies);
                var requestedAccess = new[] { FilesystemPolicy, NetworkPolicy, UiPolicy, LeastPrivilegePolicy }
                    .Where(policy => !recommended.Contains(policy, StringComparer.Ordinal))
                    .Select(GetMxcAccessDisplayName)
                    .ToList();
                return requestedAccess.Count == 0
                    ? GetResourceString("PowerScripts_MxcRecommendationMaximum")
                    : GetResourceString("PowerScripts_MxcRecommendationAccess")
                        .Replace("{0}", string.Join(", ", requestedAccess), StringComparison.Ordinal);
            }
        }

        public string MxcPolicyEditorAutomationId => $"PowerScripts_{Id}_MxcPolicyEditor";

        public string OpenFolderAutomationId => $"PowerScripts_{Id}_OpenFolder";

        public bool IsMxcPolicyOverrideEnabled(string policy) => IsMxcPolicyEffective(policy);

        public bool IsMxcPolicyEffective(string policy) => policy switch
        {
            FilesystemPolicy => IsFilesystemRestrictionEffective,
            NetworkPolicy => IsNetworkRestrictionEffective,
            UiPolicy => IsUiRestrictionEffective,
            LeastPrivilegePolicy => IsLeastPrivilegeRestrictionEffective,
            _ => false,
        };

        public bool IsMxcRestrictionConfigured(string policy) =>
            !_effectiveDisabledPolicies.Contains(policy, StringComparer.Ordinal);

        public bool IsMxcPolicyRecommended(string policy) =>
            NormalizeMxcPolicies(Mxc?.RecommendedPolicies).Contains(policy, StringComparer.Ordinal);

        public bool IsGlobalMxcPolicyEnabled(string policy) =>
            !_globalMxcDisabledPolicies.Contains(policy, StringComparer.Ordinal);

        public void ApplyLocalMxcSettings(bool globalEnabled, IEnumerable<string> globalDisabledPolicies)
        {
            if (IsMxcReviewRequired)
            {
                // Stale/unaccepted local weakening fails closed, even when global MXC is off.
                ApplyMxcSettings(true, AllMxcPolicies, Array.Empty<string>(), true, Array.Empty<string>());
            }
            else
            {
                ApplyMxcSettings(
                    LocalMxc?.Enabled,
                    LocalMxc?.EnabledPolicies,
                    LocalMxc?.DisabledPolicies,
                    globalEnabled,
                    globalDisabledPolicies);
            }
        }

        internal void ApplyMxcSettings(
            bool? enabledOverride,
            IEnumerable<string> enabledPolicies,
            IEnumerable<string> disabledPolicies,
            bool globalEnabled,
            IEnumerable<string> globalDisabledPolicies)
        {
            _mxcEnabledOverride = enabledOverride;
            _mxcEnabledPolicies = NormalizeMxcPolicies(enabledPolicies);
            _mxcDisabledPolicies = NormalizeMxcPolicies(disabledPolicies);
            _globalMxcDisabledPolicies = NormalizeMxcPolicies(globalDisabledPolicies);

            var effectiveDisabledPolicies = ResolveEffectiveDisabledPolicies(
                _globalMxcDisabledPolicies,
                _mxcEnabledPolicies,
                _mxcDisabledPolicies);
            _effectiveDisabledPolicies = effectiveDisabledPolicies.ToList();

            _isMxcEffectivelyEnabled = enabledOverride ?? globalEnabled;
            _isFilesystemRestrictionEffective = IsPolicyEffective(FilesystemPolicy, effectiveDisabledPolicies);
            _isNetworkRestrictionEffective = IsPolicyEffective(NetworkPolicy, effectiveDisabledPolicies);
            _isUiRestrictionEffective = IsPolicyEffective(UiPolicy, effectiveDisabledPolicies);
            _isLeastPrivilegeRestrictionEffective = IsPolicyEffective(LeastPrivilegePolicy, effectiveDisabledPolicies);

            if (!_isMxcEffectivelyEnabled)
            {
                _mxcEffectiveDisplay = GetResourceString("PowerScripts_MxcEffectiveDisabled");
            }
            else
            {
                var disabledDisplay = effectiveDisabledPolicies.Select(GetMxcPolicyDisplayName).ToList();
                _mxcEffectiveDisplay = disabledDisplay.Count == 0
                    ? GetResourceString("PowerScripts_MxcEffectiveAll")
                    : GetResourceString("PowerScripts_MxcEffectiveReduced")
                        .Replace("{0}", string.Join(", ", disabledDisplay), StringComparison.Ordinal);
            }

            OnPropertyChanged(nameof(MxcEnabledOverride));
            OnPropertyChanged(nameof(MxcDisabledPolicies));
            OnPropertyChanged(nameof(MxcEnabledPolicies));
            OnPropertyChanged(nameof(MxcModeIndex));
            OnPropertyChanged(nameof(IsMxcEffectivelyEnabled));
            OnPropertyChanged(nameof(CanConfigureMxcPolicies));
            OnPropertyChanged(nameof(IsFilesystemPolicyOverrideEnabled));
            OnPropertyChanged(nameof(IsNetworkPolicyOverrideEnabled));
            OnPropertyChanged(nameof(IsUiPolicyOverrideEnabled));
            OnPropertyChanged(nameof(IsLeastPrivilegePolicyOverrideEnabled));
            OnPropertyChanged(nameof(IsFilesystemRestrictionEffective));
            OnPropertyChanged(nameof(IsNetworkRestrictionEffective));
            OnPropertyChanged(nameof(IsUiRestrictionEffective));
            OnPropertyChanged(nameof(IsLeastPrivilegeRestrictionEffective));
            OnPropertyChanged(nameof(MxcEffectiveDisplay));
        }

        internal static List<string> NormalizeMxcPolicies(IEnumerable<string> policies)
        {
            var requested = policies ?? Array.Empty<string>();
            return new[] { FilesystemPolicy, NetworkPolicy, UiPolicy, LeastPrivilegePolicy }
                .Where(known => requested.Contains(known, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        public static IReadOnlyList<string> ResolveEffectiveDisabledPolicies(
            IEnumerable<string> globalDisabledPolicies,
            IEnumerable<string> scriptEnabledPolicies,
            IEnumerable<string> scriptDisabledPolicies)
        {
            var enabled = NormalizeMxcPolicies(scriptEnabledPolicies);
            var disabled = NormalizeMxcPolicies(globalDisabledPolicies);
            foreach (var policy in NormalizeMxcPolicies(scriptDisabledPolicies))
            {
                if (!disabled.Contains(policy, StringComparer.Ordinal))
                {
                    disabled.Add(policy);
                }
            }

            disabled.RemoveAll(policy => enabled.Contains(policy, StringComparer.Ordinal));
            return disabled;
        }

        internal static string GetMxcPolicyDisplayName(string policy) => policy switch
        {
            FilesystemPolicy => GetResourceString("PowerScripts_MxcPolicy_Filesystem_DisplayName"),
            NetworkPolicy => GetResourceString("PowerScripts_MxcPolicy_Network_DisplayName"),
            UiPolicy => GetResourceString("PowerScripts_MxcPolicy_Ui_DisplayName"),
            LeastPrivilegePolicy => GetResourceString("PowerScripts_MxcPolicy_LeastPrivilege_DisplayName"),
            _ => policy,
        };

        internal static string GetMxcAccessDisplayName(string policy) => policy switch
        {
            FilesystemPolicy => GetResourceString("PowerScripts_MxcAccess_Filesystem"),
            NetworkPolicy => GetResourceString("PowerScripts_MxcAccess_Network"),
            UiPolicy => GetResourceString("PowerScripts_MxcAccess_Ui"),
            LeastPrivilegePolicy => GetResourceString("PowerScripts_MxcAccess_LeastPrivilege"),
            _ => policy,
        };

        private static string GetResourceString(string resourceName) =>
            ResourceLoaderInstance.ResourceLoader.GetString(resourceName);

        private bool HasDisabledMxcPolicy(string policy) =>
            _mxcDisabledPolicies.Contains(policy, StringComparer.Ordinal);

        private bool IsPolicyEffective(string policy, IReadOnlyCollection<string> disabledPolicies) =>
            _isMxcEffectivelyEnabled && !disabledPolicies.Contains(policy, StringComparer.Ordinal);

        public sealed class MxcMetadata
        {
            public List<string> RecommendedPolicies { get; set; } =
                new() { FilesystemPolicy, NetworkPolicy, UiPolicy, LeastPrivilegePolicy };
        }
    }
}
