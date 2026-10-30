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
    /// <summary>Read-only catalogue metadata and the globally selected execution environment.</summary>
    public sealed class PowerScriptListItem : Observable
    {
        public const string FilesystemPolicy = "filesystem";
        public const string NetworkPolicy = "network";
        public const string UiPolicy = "ui";
        public const string LeastPrivilegePolicy = "leastPrivilege";

        private static readonly string[] MetadataProperties =
        {
            nameof(Id), nameof(Name), nameof(Description), nameof(HasDescriptor),
            nameof(ConfigurationErrors), nameof(HasConfigurationErrors), nameof(ConfigurationErrorDisplay),
            nameof(Runtime), nameof(HasExplicitExecution), nameof(DefaultIconGlyph),
            nameof(IconPath), nameof(IconWarning), nameof(HasIcon), nameof(HasIconWarning), nameof(IconSourcePath),
            nameof(Io), nameof(Input), nameof(Capabilities), nameof(Trusted), nameof(FolderPath), nameof(EntryFullPath),
            nameof(InputDisplay), nameof(CatalogueSummary), nameof(CardAutomationId),
            nameof(RuntimeDisplay), nameof(TrustDisplay), nameof(HasFolderPath), nameof(OpenFolderAutomationId),
        };

        private bool _iconLoadFailed;

        public string Id { get; set; } = string.Empty;

        public bool HasExplicitExecution { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool HasDescriptor { get; set; }

        public List<string> ConfigurationErrors { get; set; } = new();

        public bool HasConfigurationErrors => ConfigurationErrors is { Count: > 0 };

        public string ConfigurationErrorDisplay => string.Join(Environment.NewLine, ConfigurationErrors ?? new());

        public string Runtime { get; set; } = string.Empty;

        public string DefaultIconGlyph => string.Equals(Runtime, "Python", StringComparison.OrdinalIgnoreCase) ? "\uE943" : "\uE756";

        public string IconPath { get; set; }

        public string IconWarning { get; set; }

        public bool HasIcon => !_iconLoadFailed && !string.IsNullOrEmpty(IconPath);

        public bool HasIconWarning => !string.IsNullOrEmpty(IconWarning);

        public string IconSourcePath => HasIcon ? new Uri(IconPath).AbsoluteUri : null;

        public PowerScriptIo Io { get; set; } = new();

        public PowerScriptInput Input { get; set; }

        public List<string> Capabilities { get; set; } = new();

        public bool Trusted { get; set; }

        public string FolderPath { get; set; } = string.Empty;

        public string EntryFullPath { get; set; } = string.Empty;

        public string InputDisplay => !string.Equals(Io?.Input, "files", StringComparison.OrdinalIgnoreCase) || Input?.Extensions is not { Count: > 0 } extensions
            ? DisplayIoValue(Io?.Input)
            : extensions.Contains("*", StringComparer.Ordinal)
                ? GetResourceString("PowerScripts_IoAllFiles")
                : string.Join(", ", extensions
                    .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

        public string CatalogueSummary => $"{RuntimeDisplay} \u00b7 {InputDisplay} \u2192 {DisplayIoValue(Io?.Output)}";

        public string CardAutomationId => $"PowerScripts_{Id}_Card";

        public string RuntimeDisplay => string.IsNullOrEmpty(Runtime) ? "—" : Runtime;

        public string TrustDisplay => Trusted ? "Trusted" : "Not yet trusted — you'll be asked to allow it the first time it runs";

        public bool HasFolderPath => !string.IsNullOrEmpty(FolderPath);

        public string OpenFolderAutomationId => $"PowerScripts_{Id}_OpenFolder";

        public bool IsMxcEffectivelyEnabled { get; private set; } = true;

        public bool IsFilesystemRestrictionEffective { get; private set; } = true;

        public bool IsNetworkRestrictionEffective { get; private set; } = true;

        public bool IsUiRestrictionEffective { get; private set; } = true;

        public bool IsLeastPrivilegeRestrictionEffective { get; private set; } = true;

        public string MxcEffectiveDisplay { get; private set; } = GetResourceString("PowerScripts_MxcEffectiveAll");

        public string ExecutionSummary { get; private set; } = string.Empty;

        public string ExecutionPolicyDisplay { get; private set; } = string.Empty;

        public string ExecutionBlockedReason { get; private set; } = string.Empty;

        public bool HasExecutionBlock => !string.IsNullOrWhiteSpace(ExecutionBlockedReason);

        public bool UsesManagedLinux { get; private set; }

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
            HasDescriptor = script.HasDescriptor;
            ConfigurationErrors = script.ConfigurationErrors;
            Runtime = script.Runtime;
            HasExplicitExecution = script.HasExplicitExecution;
            IconPath = script.IconPath;
            IconWarning = script.IconWarning;
            Io = script.Io;
            Input = script.Input;
            Capabilities = script.Capabilities;
            Trusted = script.Trusted;
            FolderPath = script.FolderPath;
            EntryFullPath = script.EntryFullPath;
            foreach (var property in MetadataProperties)
            {
                OnPropertyChanged(property);
            }
        }

        public void MarkIconUnavailable()
        {
            _iconLoadFailed = true;
            IconWarning = GetResourceString("PowerScripts_IconLoadFailed");
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(IconSourcePath));
            OnPropertyChanged(nameof(IconWarning));
            OnPropertyChanged(nameof(HasIconWarning));
        }

        internal void SetExecutionSummary(string summary, string isolation, string blocked, bool managedLinux)
        {
            ExecutionSummary = summary;
            ExecutionPolicyDisplay = isolation;
            ExecutionBlockedReason = blocked;
            UsesManagedLinux = managedLinux;
            OnPropertyChanged(nameof(ExecutionSummary));
            OnPropertyChanged(nameof(ExecutionPolicyDisplay));
            OnPropertyChanged(nameof(ExecutionBlockedReason));
            OnPropertyChanged(nameof(HasExecutionBlock));
            OnPropertyChanged(nameof(UsesManagedLinux));
        }

        internal void ApplyGlobalMxcSettings(bool enabled, IEnumerable<string> disabledPolicies)
        {
            var disabled = NormalizeMxcPolicies(disabledPolicies);
            IsMxcEffectivelyEnabled = enabled;
            IsFilesystemRestrictionEffective = enabled && !disabled.Contains(FilesystemPolicy);
            IsNetworkRestrictionEffective = enabled && !disabled.Contains(NetworkPolicy);
            IsUiRestrictionEffective = enabled && !disabled.Contains(UiPolicy);
            IsLeastPrivilegeRestrictionEffective = enabled && !disabled.Contains(LeastPrivilegePolicy);
            MxcEffectiveDisplay = !enabled ? GetResourceString("PowerScripts_MxcEffectiveDisabled")
                : disabled.Count == 0 ? GetResourceString("PowerScripts_MxcEffectiveAll")
                : GetResourceString("PowerScripts_MxcEffectiveReduced")
                    .Replace("{0}", string.Join(", ", disabled.Select(GetMxcPolicyDisplayName)), StringComparison.Ordinal);
            OnPropertyChanged(nameof(IsMxcEffectivelyEnabled));
            OnPropertyChanged(nameof(IsFilesystemRestrictionEffective));
            OnPropertyChanged(nameof(IsNetworkRestrictionEffective));
            OnPropertyChanged(nameof(IsUiRestrictionEffective));
            OnPropertyChanged(nameof(IsLeastPrivilegeRestrictionEffective));
            OnPropertyChanged(nameof(MxcEffectiveDisplay));
        }

        internal static List<string> NormalizeMxcPolicies(IEnumerable<string> policies) =>
            new[] { FilesystemPolicy, NetworkPolicy, UiPolicy, LeastPrivilegePolicy }
                .Where(known => (policies ?? Array.Empty<string>()).Contains(known, StringComparer.OrdinalIgnoreCase)).ToList();

        internal static string GetMxcPolicyDisplayName(string policy) => policy switch
        {
            FilesystemPolicy => GetResourceString("PowerScripts_MxcPolicy_Filesystem_DisplayName"),
            NetworkPolicy => GetResourceString("PowerScripts_MxcPolicy_Network_DisplayName"),
            UiPolicy => GetResourceString("PowerScripts_MxcPolicy_Ui_DisplayName"),
            LeastPrivilegePolicy => GetResourceString("PowerScripts_MxcPolicy_LeastPrivilege_DisplayName"),
            _ => policy,
        };

        private static string DisplayIoValue(string value) => value?.ToLowerInvariant() switch
        {
            null or "" or "none" => GetResourceString("PowerScripts_IoNone"),
            "files" => GetResourceString("PowerScripts_IoFiles"),
            "text" => GetResourceString("PowerScripts_IoText"),
            "image" => GetResourceString("PowerScripts_IoImage"),
            "audio" => GetResourceString("PowerScripts_IoAudio"),
            _ => value,
        };

        private static string GetResourceString(string resourceName) => ResourceLoaderInstance.ResourceLoader.GetString(resourceName);
    }
}
