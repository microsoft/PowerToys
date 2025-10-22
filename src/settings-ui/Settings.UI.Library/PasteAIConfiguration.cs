// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Configuration for Paste AI features (custom action transformations like custom prompt processing)
    /// </summary>
    public class PasteAIConfiguration : INotifyPropertyChanged
    {
        private string _activeProviderId = string.Empty;
        private ObservableCollection<PasteAIProviderDefinition> _providers = new();
        private bool _useSharedCredentials = true;
        private string _legacyServiceType = "OpenAI";
        private string _legacyModelName = "gpt-3.5-turbo";
        private string _legacyEndpointUrl = string.Empty;
        private string _legacyApiVersion = string.Empty;
        private string _legacyDeploymentName = string.Empty;
        private string _legacyModelPath = string.Empty;
        private string _legacySystemPrompt = string.Empty;
        private bool _legacyModerationEnabled = true;
        private Dictionary<string, AIProviderConfigurationSnapshot> _legacyProviderConfigurations;

        public event PropertyChangedEventHandler PropertyChanged;

        [JsonPropertyName("active-provider-id")]
        public string ActiveProviderId
        {
            get => _activeProviderId;
            set => SetProperty(ref _activeProviderId, value ?? string.Empty);
        }

        [JsonPropertyName("providers")]
        public ObservableCollection<PasteAIProviderDefinition> Providers
        {
            get => _providers;
            set => SetProperty(ref _providers, value ?? new ObservableCollection<PasteAIProviderDefinition>());
        }

        [JsonPropertyName("use-shared-credentials")]
        public bool UseSharedCredentials
        {
            get => _useSharedCredentials;
            set => SetProperty(ref _useSharedCredentials, value);
        }

        // Legacy properties retained for migration. They will be cleared once converted to the new format.
        [JsonPropertyName("service-type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyServiceType
        {
            get => _legacyServiceType;
            set => _legacyServiceType = value;
        }

        [JsonPropertyName("model-name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyModelName
        {
            get => _legacyModelName;
            set => _legacyModelName = value;
        }

        [JsonPropertyName("endpoint-url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyEndpointUrl
        {
            get => _legacyEndpointUrl;
            set => _legacyEndpointUrl = value;
        }

        [JsonPropertyName("api-version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyApiVersion
        {
            get => _legacyApiVersion;
            set => _legacyApiVersion = value;
        }

        [JsonPropertyName("deployment-name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyDeploymentName
        {
            get => _legacyDeploymentName;
            set => _legacyDeploymentName = value;
        }

        [JsonPropertyName("model-path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyModelPath
        {
            get => _legacyModelPath;
            set => _legacyModelPath = value;
        }

        [JsonPropertyName("system-prompt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacySystemPrompt
        {
            get => _legacySystemPrompt;
            set => _legacySystemPrompt = value;
        }

        [JsonPropertyName("moderation-enabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool LegacyModerationEnabled
        {
            get => _legacyModerationEnabled;
            set => _legacyModerationEnabled = value;
        }

        [JsonPropertyName("provider-configurations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, AIProviderConfigurationSnapshot> LegacyProviderConfigurations
        {
            get => _legacyProviderConfigurations;
            set => _legacyProviderConfigurations = value;
        }

        [JsonIgnore]
        public PasteAIProviderDefinition ActiveProvider
        {
            get
            {
                if (_providers is null || _providers.Count == 0)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(_activeProviderId))
                {
                    var match = _providers.FirstOrDefault(provider => string.Equals(provider.Id, _activeProviderId, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        return match;
                    }
                }

                return _providers[0];
            }
        }

        [JsonIgnore]
        public AIServiceType ActiveServiceTypeKind => ActiveProvider?.ServiceTypeKind ?? AIServiceType.OpenAI;

        public override string ToString()
            => JsonSerializer.Serialize(this);

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
