// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class McpViewModel : Observable
    {
        private bool _isEnabled;
        private bool _registerToVSCode;
        private bool _registerToWindowsCopilot;
        private bool _awakeModuleEnabled;
        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;

        public McpViewModel()
        {
            _isEnabled = false;
            _registerToVSCode = false;
            _registerToWindowsCopilot = false;
            _awakeModuleEnabled = true;
        }

        public bool IsEnabled
        {
            get
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return _enabledGPOConfiguration;
                }
                else
                {
                    return _isEnabled;
                }
            }

            set
            {
                if (_isEnabled != value)
                {
                    if (_enabledStateIsGPOConfigured)
                    {
                        // If it's GPO configured, shouldn't be able to change this state.
                        return;
                    }

                    _isEnabled = value;
                    RefreshEnabledState();
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
            set
            {
                if (_enabledStateIsGPOConfigured != value)
                {
                    _enabledStateIsGPOConfigured = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnabledGPOConfiguration
        {
            get => _enabledGPOConfiguration;
            set
            {
                if (_enabledGPOConfiguration != value)
                {
                    _enabledGPOConfiguration = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool RegisterToVSCode
        {
            get => _registerToVSCode;
            set
            {
                if (_registerToVSCode != value)
                {
                    _registerToVSCode = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool RegisterToWindowsCopilot
        {
            get => _registerToWindowsCopilot;
            set
            {
                if (_registerToWindowsCopilot != value)
                {
                    _registerToWindowsCopilot = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool AwakeModuleEnabled
        {
            get => _awakeModuleEnabled;
            set
            {
                if (_awakeModuleEnabled != value)
                {
                    _awakeModuleEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void RefreshEnabledState()
        {
            OnPropertyChanged(nameof(IsEnabled));
        }

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            OnPropertyChanged(propertyName);
        }
    }
}
