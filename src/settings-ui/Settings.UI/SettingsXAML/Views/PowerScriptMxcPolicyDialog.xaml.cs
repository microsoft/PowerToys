// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerScriptMxcPolicyDialog : ContentDialog
    {
        private bool _usesGlobalPolicy;
        private bool _isSaving;

        public PowerScriptMxcPolicyDialog(PowerScriptListItem script)
        {
            ArgumentNullException.ThrowIfNull(script);

            Script = script;
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Policies = new ObservableCollection<PowerScriptMxcPolicyOption>
            {
                CreateOption(
                    PowerScriptListItem.FilesystemPolicy,
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Filesystem_Title"),
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Filesystem_Description"),
                    resourceLoader),
                CreateOption(
                    PowerScriptListItem.NetworkPolicy,
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Network_Title"),
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Network_Description"),
                    resourceLoader),
                CreateOption(
                    PowerScriptListItem.UiPolicy,
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Ui_Title"),
                    resourceLoader.GetString("PowerScripts_MxcPolicy_Ui_Description"),
                    resourceLoader),
                CreateOption(
                    PowerScriptListItem.LeastPrivilegePolicy,
                    resourceLoader.GetString("PowerScripts_MxcPolicy_LeastPrivilege_Title"),
                    resourceLoader.GetString("PowerScripts_MxcPolicy_LeastPrivilege_Description"),
                    resourceLoader),
            };

            InitializeComponent();

            Title = resourceLoader.GetString("PowerScripts_MxcPolicyDialog_Title")
                .Replace("{0}", script.Name, StringComparison.Ordinal);
            PrimaryButtonText = resourceLoader.GetString("PowerScripts_MxcPolicyDialog_Apply");
            CloseButtonText = resourceLoader.GetString("PowerScripts_MxcPolicyDialog_Cancel");
            MxcModeComboBox.SelectedIndex = IsManagedLinux ? 1 : script.MxcModeIndex;
            _usesGlobalPolicy = !IsManagedLinux && script.MxcModeIndex == 0 &&
                                script.MxcEnabledPolicies.Count == 0 &&
                                script.MxcDisabledPolicies.Count == 0;
            UpdateRiskState(resetAcceptance: false);
            Closing += (_, args) => args.Cancel = _isSaving;
        }

        public Func<Task> SaveAsync { get; set; }

        public PowerScriptListItem Script { get; }

        public ObservableCollection<PowerScriptMxcPolicyOption> Policies { get; }

        public bool IsManagedLinux => Script.UsesManagedLinux;

        public Visibility WindowsControlsVisibility => IsManagedLinux ? Visibility.Collapsed : Visibility.Visible;

        public Visibility LinuxControlsVisibility => IsManagedLinux ? Visibility.Visible : Visibility.Collapsed;

        public IEnumerable<PowerScriptMxcPolicyOption> VisiblePolicies =>
            IsManagedLinux ? Policies.Where(policy => policy.Key == PowerScriptListItem.NetworkPolicy) : Policies;

        public int MxcModeIndex => MxcModeComboBox.SelectedIndex;

        public bool RiskAccepted => RiskAcceptanceCheckBox.IsChecked == true;

        public bool UsesGlobalPolicy => _usesGlobalPolicy;

        public string[] EnabledPolicies =>
            Policies.Where(policy => policy.IsEnabled).Select(policy => policy.Key).ToArray();

        private PowerScriptMxcPolicyOption CreateOption(
            string key,
            string title,
            string description,
            Microsoft.Windows.ApplicationModel.Resources.ResourceLoader resourceLoader) =>
            new(
                key,
                title,
                description,
                resourceLoader.GetString(
                    Script.IsMxcPolicyRecommended(key)
                        ? "PowerScripts_MxcPolicy_RecommendedKeep"
                        : "PowerScripts_MxcPolicy_RecommendedAllow"),
                Script.IsMxcRestrictionConfigured(key),
                Script.IsMxcPolicyRecommended(key),
                Script.IsGlobalMxcPolicyEnabled(key));

        private void UseRecommendedPolicy_Click(object sender, RoutedEventArgs e)
        {
            MxcModeComboBox.SelectedIndex = 1;
            foreach (var policy in Policies)
            {
                policy.IsEnabled = policy.IsRecommended;
            }

            _usesGlobalPolicy = false;
            UpdateRiskState();
        }

        private void UseMaximumIsolation_Click(object sender, RoutedEventArgs e)
        {
            MxcModeComboBox.SelectedIndex = 1;
            foreach (var policy in Policies)
            {
                policy.IsEnabled = true;
            }

            _usesGlobalPolicy = false;
            UpdateRiskState();
        }

        private void UseGlobalPolicy_Click(object sender, RoutedEventArgs e)
        {
            MxcModeComboBox.SelectedIndex = 0;
            foreach (var policy in Policies)
            {
                policy.IsEnabled = policy.IsGloballyEnabled;
            }

            _usesGlobalPolicy = true;
            UpdateRiskState();
        }

        private void UseLinuxPolicy_Click(object sender, RoutedEventArgs e)
        {
            MxcModeComboBox.SelectedIndex = 1;
            foreach (var policy in Policies)
            {
                policy.IsEnabled = policy.Key is PowerScriptListItem.FilesystemPolicy or PowerScriptListItem.NetworkPolicy;
            }

            _usesGlobalPolicy = false;
            UpdateRiskState();
        }

        private void PolicySelectionChanged(object sender, RoutedEventArgs e)
        {
            _usesGlobalPolicy = false;
            UpdateRiskState();
        }

        private void RiskAcceptanceChanged(object sender, RoutedEventArgs e) => UpdateRiskState(resetAcceptance: false);

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (RequiresRiskAcceptance() && !RiskAccepted)
            {
                args.Cancel = true;
                return;
            }

            if (SaveAsync is null)
            {
                return;
            }

            var deferral = args.GetDeferral();
            try
            {
                _isSaving = true;
                IsPrimaryButtonEnabled = false;
                ((Control)Content).IsEnabled = false;
                SaveError.IsOpen = false;
                await SaveAsync();
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                SaveError.Message = ex.Message;
                SaveError.IsOpen = true;
            }
            finally
            {
                _isSaving = false;
                ((Control)Content).IsEnabled = true;
                UpdateRiskState(resetAcceptance: false);
                deferral.Complete();
            }
        }

        private bool RequiresRiskAcceptance() =>
            !_usesGlobalPolicy &&
            (MxcModeComboBox.SelectedIndex == 2 || Policies.Any(policy => !policy.IsEnabled));

        private void UpdateRiskState(bool resetAcceptance = true)
        {
            if (ReducedIsolationWarning is null || RiskAcceptanceCheckBox is null)
            {
                return;
            }

            var requiresAcceptance = RequiresRiskAcceptance();
            if (resetAcceptance)
            {
                RiskAcceptanceCheckBox.IsChecked = false;
            }

            ReducedIsolationWarning.IsOpen = requiresAcceptance;
            var supported = !IsManagedLinux ||
                (MxcModeIndex == 1 &&
                 Policies.Single(policy => policy.Key == PowerScriptListItem.FilesystemPolicy).IsEnabled &&
                 !Policies.Single(policy => policy.Key == PowerScriptListItem.UiPolicy).IsEnabled &&
                 !Policies.Single(policy => policy.Key == PowerScriptListItem.LeastPrivilegePolicy).IsEnabled);
            IsPrimaryButtonEnabled = supported && (!requiresAcceptance || RiskAcceptanceCheckBox.IsChecked == true);
            DefaultButton = requiresAcceptance ? ContentDialogButton.Close : ContentDialogButton.Primary;
        }
    }
}
