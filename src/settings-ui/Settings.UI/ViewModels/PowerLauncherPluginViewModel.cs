// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class PowerLauncherPluginViewModel : INotifyPropertyChanged
    {
        private readonly PowerLauncherPluginSettings settings;
        private readonly Func<bool> isDark;

        public PowerLauncherPluginViewModel(PowerLauncherPluginSettings settings, Func<bool> isDark)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings), "PowerLauncherPluginSettings object is null");
            }

            this.settings = settings;
            this.isDark = isDark;
            foreach (var item in AdditionalOptions)
            {
                item.PropertyChanged += (object sender, PropertyChangedEventArgs e) =>
                {
                    NotifyPropertyChanged(nameof(AdditionalOptions));
                };
            }

            _enabledGpoRuleConfiguration = (GpoRuleConfigured)settings.EnabledPolicyUiState;
            _enabledGpoRuleIsConfigured = _enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;

            _hasValidWebsiteUri = Uri.IsWellFormedUriString(settings.Website, UriKind.Absolute);
            _websiteUri = _hasValidWebsiteUri ? settings.Website : WebsiteFallbackUri;
        }

        public string Id { get => settings.Id; }

        public string Name { get => settings.Name; }

        public string Description { get => settings.Description; }

        public string Version { get => settings.Version; }

        public string Author { get => settings.Author; }

        // Fallback value for the website in case the uri from json is not well formatted
        private const string WebsiteFallbackUri = "https://aka.ms/PowerToys";
        private string _websiteUri;
        private bool _hasValidWebsiteUri;

        public string WebsiteUri => _websiteUri;

        public bool HasValidWebsiteUri => _hasValidWebsiteUri;

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledGpoRuleIsConfigured;

        public bool Disabled
        {
            get
            {
                if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled)
                {
                    return true;
                }
                else if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
                {
                    return false;
                }
                else
                {
                    return settings.Disabled;
                }
            }

            set
            {
                if (settings.Disabled != value)
                {
                    settings.Disabled = value;

                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowNotAccessibleWarning));
                    NotifyPropertyChanged(nameof(Enabled));
                    NotifyPropertyChanged(nameof(DisabledOpacity));
                    NotifyPropertyChanged(nameof(IsGlobalAndEnabled));
                    NotifyPropertyChanged(nameof(ShowBadgeOnPluginSettingError));
                }
            }
        }

        public bool Enabled => !Disabled;

        public bool EnabledGpoRuleIsConfigured => _enabledGpoRuleIsConfigured;

        public double DisabledOpacity => Disabled ? 0.5 : 1;

        public bool IsGlobalAndEnabled
        {
            get
            {
                return IsGlobal && Enabled;
            }
        }

        public bool IsGlobal
        {
            get
            {
                return settings.IsGlobal;
            }

            set
            {
                if (settings.IsGlobal != value)
                {
                    settings.IsGlobal = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowNotAccessibleWarning));
                    NotifyPropertyChanged(nameof(IsGlobalAndEnabled));
                    NotifyPropertyChanged(nameof(ShowBadgeOnPluginSettingError));
                }
            }
        }

        public int WeightBoost
        {
            get
            {
                return settings.WeightBoost;
            }

            set
            {
                if (settings.WeightBoost != value)
                {
                    settings.WeightBoost = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string ActionKeyword
        {
            get
            {
                return settings.ActionKeyword;
            }

            set
            {
                if (settings.ActionKeyword != value)
                {
                    settings.ActionKeyword = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowNotAccessibleWarning));
                    NotifyPropertyChanged(nameof(ShowBadgeOnPluginSettingError));
                }
            }
        }

        private IEnumerable<PluginAdditionalOptionViewModel> _additionalOptions;

        public IEnumerable<PluginAdditionalOptionViewModel> AdditionalOptions
        {
            get
            {
                if (_additionalOptions == null)
                {
                    _additionalOptions = settings.AdditionalOptions.Select(x => new PluginAdditionalOptionViewModel(x)).ToList();
                }

                return _additionalOptions;
            }
        }

        public bool ShowAdditionalOptions
        {
            get => AdditionalOptions.Any();
        }

        public override string ToString()
        {
            return $"{Name}. {Description}";
        }

#nullable enable
        public Uri? IconPath => Uri.TryCreate(isDark() ? settings.IconPathDark : settings.IconPathLight, UriKind.Absolute, out var uri) ? uri : null;
#nullable restore

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool ShowNotAccessibleWarning
        {
            get => !Disabled && !IsGlobal && string.IsNullOrWhiteSpace(ActionKeyword);
        }

        // The Badge is shown in case of ANY error event, but NEVER when the plugin is disabled.
        // Logic = !disabled && (errorA or errorB or errorC...)
        // Current count of possible error events: 1 (NotAccessible)
        public bool ShowBadgeOnPluginSettingError
        {
            get => !Disabled && ShowNotAccessibleWarning;
        }
    }
}
