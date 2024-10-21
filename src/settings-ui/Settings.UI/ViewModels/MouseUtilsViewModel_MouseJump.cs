// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MouseUtilsViewModel : Observable
    {
        private GpoRuleConfigured _jumpEnabledGpoRuleConfiguration;
        private bool _jumpEnabledStateIsGPOConfigured;
        private bool _isMouseJumpEnabled;

        internal MouseJumpSettings MouseJumpSettingsConfig { get; set; }

        private void InitializeMouseJumpSettings(ISettingsRepository<MouseJumpSettings> mouseJumpSettingsRepository)
        {
            ArgumentNullException.ThrowIfNull(mouseJumpSettingsRepository);
            this.MouseJumpSettingsConfig = mouseJumpSettingsRepository.SettingsConfig;
            this.MouseJumpSettingsConfig.Properties.ThumbnailSize.PropertyChanged += this.MouseJumpThumbnailSizePropertyChanged;
        }

        private void InitializeMouseJumpEnabledValues()
        {
            _jumpEnabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMouseJumpEnabledValue();
            if (_jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _jumpEnabledStateIsGPOConfigured = true;
                _isMouseJumpEnabled = _jumpEnabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isMouseJumpEnabled = GeneralSettingsConfig.Enabled.MouseJump;
            }
        }

        public bool IsMouseJumpEnabled
        {
            get => _isMouseJumpEnabled;
            set
            {
                if (_jumpEnabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isMouseJumpEnabled != value)
                {
                    _isMouseJumpEnabled = value;

                    GeneralSettingsConfig.Enabled.MouseJump = value;
                    OnPropertyChanged(nameof(_isMouseJumpEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public bool IsJumpEnabledGpoConfigured
        {
            get => _jumpEnabledStateIsGPOConfigured;
        }

        public HotkeySettings MouseJumpActivationShortcut
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.ActivationShortcut;
            }

            set
            {
                if (MouseJumpSettingsConfig.Properties.ActivationShortcut != value)
                {
                    MouseJumpSettingsConfig.Properties.ActivationShortcut = value ?? MouseJumpSettingsConfig.Properties.DefaultActivationShortcut;
                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public MouseJumpThumbnailSize MouseJumpThumbnailSize
        {
            get
            {
                return MouseJumpSettingsConfig.Properties.ThumbnailSize;
            }

            set
            {
                if ((MouseJumpSettingsConfig.Properties.ThumbnailSize.Width != value?.Width)
                    && (MouseJumpSettingsConfig.Properties.ThumbnailSize.Height != value?.Height))
                {
                    MouseJumpSettingsConfig.Properties.ThumbnailSize = value;
                    NotifyMouseJumpPropertyChanged();
                }
            }
        }

        public void MouseJumpThumbnailSizePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyMouseJumpPropertyChanged(nameof(MouseJumpThumbnailSize));
        }

        public void NotifyMouseJumpPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SndMouseJumpSettings outsettings = new SndMouseJumpSettings(MouseJumpSettingsConfig);
            SndModuleSettings<SndMouseJumpSettings> ipcMessage = new SndModuleSettings<SndMouseJumpSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(MouseJumpSettingsConfig.ToJsonString(), MouseJumpSettings.ModuleName);
        }
    }
}
