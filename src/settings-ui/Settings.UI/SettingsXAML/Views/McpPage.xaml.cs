// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class McpPage : NavigablePage, IRefreshablePage
    {
        private readonly SettingsUtils _settingsUtils;

        private readonly SettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly SettingsRepository<McpSettings> _moduleSettingsRepository;

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly Func<string, int> _sendConfigMsg;

        private McpViewModel ViewModel { get; set; }

        public McpPage()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _settingsUtils = new SettingsUtils();
            _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            ViewModel = new McpViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            _moduleSettingsRepository = SettingsRepository<McpSettings>.GetInstance(_settingsUtils);

            // We load the view model settings first.
            LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);

            this.InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void LoadSettings(
            SettingsRepository<GeneralSettings> generalSettingsRepository,
            SettingsRepository<McpSettings> moduleSettingsRepository)
        {
            var generalSettings = generalSettingsRepository.SettingsConfig;
            var moduleSettings = moduleSettingsRepository.SettingsConfig;

            ViewModel.IsEnabled = generalSettings.Enabled.MCP;
            ViewModel.RegisterToVSCode = moduleSettings.Properties.RegisterToVSCode;
            ViewModel.RegisterToWindowsCopilot = moduleSettings.Properties.RegisterToWindowsCopilot;
            ViewModel.AwakeModuleEnabled = moduleSettings.Properties.EnabledModules.TryGetValue("Awake", out bool awakeEnabled) ? awakeEnabled : true;

            // TODO: Uncomment when GPO support is implemented
            // ViewModel.IsEnabledGpoConfigured = GPOWrapper.GetConfiguredMcpEnabledValue() == GpoRuleConfigured.Enabled || GPOWrapper.GetConfiguredMcpEnabledValue() == GpoRuleConfigured.Disabled;
            ViewModel.IsEnabledGpoConfigured = false;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsEnabled):
                    {
                        var generalSettings = _generalSettingsRepository.SettingsConfig;
                        generalSettings.Enabled.MCP = ViewModel.IsEnabled;
                        OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(generalSettings);
                        _sendConfigMsg(outgoing.ToString());
                    }

                    break;
                case nameof(ViewModel.RegisterToVSCode):
                    {
                        // Update VS Code registration
                        bool success = McpRegistrationHelper.UpdateVSCodeRegistration(ViewModel.RegisterToVSCode);
                        if (!success && ViewModel.RegisterToVSCode)
                        {
                            // If registration failed, revert the toggle
                            ViewModel.RegisterToVSCode = false;
                        }

                        var moduleSettings = _moduleSettingsRepository.SettingsConfig;
                        moduleSettings.Properties.RegisterToVSCode = ViewModel.RegisterToVSCode;
                        _settingsUtils.SaveSettings(moduleSettings.ToJsonString(), McpSettings.ModuleName);
                    }

                    break;
                case nameof(ViewModel.RegisterToWindowsCopilot):
                    {
                        // Update Windows Copilot registration
                        bool success = McpRegistrationHelper.UpdateWindowsCopilotRegistration(ViewModel.RegisterToWindowsCopilot);
                        if (!success && ViewModel.RegisterToWindowsCopilot)
                        {
                            // If registration failed, revert the toggle
                            ViewModel.RegisterToWindowsCopilot = false;
                        }

                        var moduleSettings = _moduleSettingsRepository.SettingsConfig;
                        moduleSettings.Properties.RegisterToWindowsCopilot = ViewModel.RegisterToWindowsCopilot;
                        _settingsUtils.SaveSettings(moduleSettings.ToJsonString(), McpSettings.ModuleName);
                    }

                    break;
                case nameof(ViewModel.AwakeModuleEnabled):
                    {
                        var moduleSettings = _moduleSettingsRepository.SettingsConfig;
                        moduleSettings.Properties.EnabledModules["Awake"] = ViewModel.AwakeModuleEnabled;
                        _settingsUtils.SaveSettings(moduleSettings.ToJsonString(), McpSettings.ModuleName);
                    }

                    break;
            }
        }
    }
}
