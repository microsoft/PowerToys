// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A single PowerScript shown in the Settings list. This is a read-only projection of the
    /// script's header metadata (the source of truth), as emitted by
    /// <c>PowerScripts.Host.exe list --json</c>. The Settings page only displays this information;
    /// authors change it by editing the script file's <c>@powerscript.*</c> header.
    /// </summary>
    public sealed class PowerScriptListItem : Observable
    {
        public const string FilesystemPolicy = "filesystem";
        public const string NetworkPolicy = "network";
        public const string UiPolicy = "ui";
        public const string LeastPrivilegePolicy = "leastPrivilege";

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

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Runtime { get; set; } = string.Empty;

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

        public string KindGlyph => IsFileScript
            ? "\uE8A5" // file action
            : "\uE756"; // system action

        /// <summary>True for file scripts, which can be triggered from the Explorer right-click menu.</summary>
        public bool IsFileScript => string.Equals(Io?.Input, "files", StringComparison.OrdinalIgnoreCase);

        public string KindDisplay => IsFileScript ? "File" : "System";

        /// <summary>Comma-separated trigger extensions declared in the manifest (file scripts only).</summary>
        public string ExtensionsDisplay => Input?.Extensions is { Count: > 0 } exts
            ? string.Join(", ", exts)
            : "—";

        /// <summary>Resolved input/output contract used by consumers to discover compatible scripts.</summary>
        public string IoDisplay => $"{DisplayIoValue(Io?.Input)} \u2192 {DisplayIoValue(Io?.Output)}";

        /// <summary>Comma-separated list of the capabilities the script declares.</summary>
        public string CapabilitiesDisplay => Capabilities is { Count: > 0 }
            ? string.Join(", ", Capabilities)
            : "—";

        /// <summary>Friendly runtime label (e.g. "PowerShell").</summary>
        public string RuntimeDisplay => string.IsNullOrEmpty(Runtime) ? "—" : Runtime;

        private static string DisplayIoValue(string value) =>
            string.IsNullOrWhiteSpace(value) ? "none" : value;

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
