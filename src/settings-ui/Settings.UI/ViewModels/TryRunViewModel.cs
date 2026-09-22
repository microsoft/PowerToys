// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class TryRunViewModel : Observable
    {
        private readonly ISettingsRepository<GeneralSettings> generalRepository;
        private readonly TryRunSettings settings;
        private readonly Func<string, int> sendConfigMessage;
        private readonly bool isElevated;
        private readonly Func<GpoRuleConfigured> getGpoConfiguration;
        private GpoRuleConfigured gpoConfiguration;
        private bool isEnabled;

        public TryRunViewModel(ISettingsRepository<GeneralSettings> generalRepository, ISettingsRepository<TryRunSettings> settingsRepository, Func<string, int> sendConfigMessage, bool isElevated = false, GpoRuleConfigured? globalGpoConfiguration = null)
        {
            ArgumentNullException.ThrowIfNull(generalRepository);
            ArgumentNullException.ThrowIfNull(settingsRepository);
            ArgumentNullException.ThrowIfNull(sendConfigMessage);
            this.generalRepository = generalRepository;
            settings = settingsRepository.SettingsConfig;
            this.sendConfigMessage = sendConfigMessage;
            this.isElevated = isElevated;
            getGpoConfiguration = globalGpoConfiguration is { } configured ? () => configured : GPOWrapper.GetConfiguredGlobalUtilityEnabledValue;
            InitializeEnabledState();
        }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public bool IsElevated => isElevated;

        public bool CanConfigure => !isElevated;

        public bool IsEnabledGpoConfigured => gpoConfiguration is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled;

        public bool CanChangeEnabled => CanConfigure && !IsEnabledGpoConfigured;

        public bool CanLaunch => IsEnabled && CanConfigure;

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (!CanChangeEnabled)
                {
                    return;
                }

                if (isEnabled != value)
                {
                    isEnabled = value;
                    generalRepository.SettingsConfig.Enabled.TryRun = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanLaunch));
                    sendConfigMessage(new OutGoingGeneralSettings(generalRepository.SettingsConfig).ToString());
                }
            }
        }

        public bool ShowInContextMenu
        {
            get => settings.Properties.ShowInContextMenu.Value;
            set
            {
                if (!CanConfigure || gpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return;
                }

                if (settings.Properties.ShowInContextMenu.Value != value)
                {
                    settings.Properties.ShowInContextMenu.Value = value;
                    OnPropertyChanged();
                    sendConfigMessage(string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"powertoys\":{{\"{0}\":{1}}}}}",
                        TryRunSettings.ModuleName,
                        JsonSerializer.Serialize(settings, SourceGenerationContextContext.Default.TryRunSettings)));
                }
            }
        }

        public void Launch()
        {
            if (CanLaunch)
            {
                sendConfigMessage("{\"action\":{\"TryRun\":{\"action_name\":\"Launch\",\"value\":\"\"}}}");
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledState();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(CanLaunch));
            OnPropertyChanged(nameof(CanChangeEnabled));
            OnPropertyChanged(nameof(IsEnabledGpoConfigured));
        }

        private void InitializeEnabledState()
        {
            gpoConfiguration = getGpoConfiguration();
            isEnabled = !isElevated && (gpoConfiguration == GpoRuleConfigured.Enabled || (gpoConfiguration != GpoRuleConfigured.Disabled && generalRepository.SettingsConfig.Enabled.TryRun));
        }
    }
}
