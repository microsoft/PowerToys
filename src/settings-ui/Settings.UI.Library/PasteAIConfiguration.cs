// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private string _serviceType = "OpenAI";
        private string _modelName = "gpt-3.5-turbo";
        private string _endpointUrl = string.Empty;
        private string _apiVersion = string.Empty;
        private string _deploymentName = string.Empty;
        private string _modelPath = string.Empty;
        private bool _useSharedCredentials = true;
        private string _systemPrompt = string.Empty;
        private bool _moderationEnabled = true;
        private Dictionary<string, AIProviderConfigurationSnapshot> _providerConfigurations = new(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;

        [JsonPropertyName("service-type")]
        public string ServiceType
        {
            get => _serviceType;
            set => SetProperty(ref _serviceType, value);
        }

        [JsonIgnore]
        public AIServiceType ServiceTypeKind
        {
            get => _serviceType.ToAIServiceType();
            set => ServiceType = value.ToConfigurationString();
        }

        [JsonPropertyName("model-name")]
        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

        [JsonPropertyName("endpoint-url")]
        public string EndpointUrl
        {
            get => _endpointUrl;
            set => SetProperty(ref _endpointUrl, value);
        }

        [JsonPropertyName("api-version")]
        public string ApiVersion
        {
            get => _apiVersion;
            set => SetProperty(ref _apiVersion, value);
        }

        [JsonPropertyName("deployment-name")]
        public string DeploymentName
        {
            get => _deploymentName;
            set => SetProperty(ref _deploymentName, value);
        }

        [JsonPropertyName("model-path")]
        public string ModelPath
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }

        [JsonPropertyName("use-shared-credentials")]
        public bool UseSharedCredentials
        {
            get => _useSharedCredentials;
            set => SetProperty(ref _useSharedCredentials, value);
        }

        [JsonPropertyName("system-prompt")]
        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value?.Trim() ?? string.Empty);
        }

        [JsonPropertyName("moderation-enabled")]
        public bool ModerationEnabled
        {
            get => _moderationEnabled;
            set => SetProperty(ref _moderationEnabled, value);
        }

        [JsonPropertyName("provider-configurations")]
        public Dictionary<string, AIProviderConfigurationSnapshot> ProviderConfigurations
        {
            get => _providerConfigurations;
            set => SetProperty(ref _providerConfigurations, value ?? new Dictionary<string, AIProviderConfigurationSnapshot>(StringComparer.OrdinalIgnoreCase));
        }

        public bool HasProviderConfiguration(string serviceType)
        {
            return _providerConfigurations.ContainsKey(NormalizeServiceType(serviceType));
        }

        public AIProviderConfigurationSnapshot GetOrCreateProviderConfiguration(string serviceType)
        {
            var key = NormalizeServiceType(serviceType);
            if (!_providerConfigurations.TryGetValue(key, out var snapshot))
            {
                snapshot = new AIProviderConfigurationSnapshot();
                _providerConfigurations[key] = snapshot;
                OnPropertyChanged(nameof(ProviderConfigurations));
            }

            return snapshot;
        }

        public void SetProviderConfiguration(string serviceType, AIProviderConfigurationSnapshot snapshot)
        {
            _providerConfigurations[NormalizeServiceType(serviceType)] = snapshot ?? new AIProviderConfigurationSnapshot();
            OnPropertyChanged(nameof(ProviderConfigurations));
        }

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

        private static string NormalizeServiceType(string serviceType)
        {
            return string.IsNullOrWhiteSpace(serviceType) ? "OpenAI" : serviceType;
        }
    }
}
