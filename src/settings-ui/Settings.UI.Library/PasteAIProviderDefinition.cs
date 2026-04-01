// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Represents a single Paste AI provider configuration entry.
    /// </summary>
    public class PasteAIProviderDefinition : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _serviceType = "OpenAI";
        private string _modelName = string.Empty;
        private string _endpointUrl = string.Empty;
        private string _apiVersion = string.Empty;
        private string _deploymentName = string.Empty;
        private string _modelPath = string.Empty;
        private string _systemPrompt = string.Empty;
        private bool _moderationEnabled = true;
        private bool _isActive;
        private bool _enableAdvancedAI;
        private bool _isLocalModel;

        public event PropertyChangedEventHandler PropertyChanged;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("service-type")]
        public string ServiceType
        {
            get => _serviceType;
            set
            {
                if (SetProperty(ref _serviceType, string.IsNullOrWhiteSpace(value) ? "OpenAI" : value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        [JsonIgnore]
        public AIServiceType ServiceTypeKind
        {
            get => ServiceType.ToAIServiceType();
            set => ServiceType = value.ToConfigurationString();
        }

        [JsonPropertyName("model-name")]
        public string ModelName
        {
            get => _modelName;
            set
            {
                if (SetProperty(ref _modelName, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        [JsonPropertyName("endpoint-url")]
        public string EndpointUrl
        {
            get => _endpointUrl;
            set => SetProperty(ref _endpointUrl, value ?? string.Empty);
        }

        [JsonPropertyName("api-version")]
        public string ApiVersion
        {
            get => _apiVersion;
            set => SetProperty(ref _apiVersion, value ?? string.Empty);
        }

        [JsonPropertyName("deployment-name")]
        public string DeploymentName
        {
            get => _deploymentName;
            set => SetProperty(ref _deploymentName, value ?? string.Empty);
        }

        [JsonPropertyName("model-path")]
        public string ModelPath
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value ?? string.Empty);
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

        [JsonPropertyName("enable-advanced-ai")]
        public bool EnableAdvancedAI
        {
            get => _enableAdvancedAI;
            set => SetProperty(ref _enableAdvancedAI, value);
        }

        [JsonPropertyName("is-local-model")]
        public bool IsLocalModel
        {
            get => _isLocalModel;
            set => SetProperty(ref _isLocalModel, value);
        }

        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(ModelName) ? ServiceType : ModelName;

        public PasteAIProviderDefinition Clone()
        {
            return new PasteAIProviderDefinition
            {
                Id = Id,
                ServiceType = ServiceType,
                ModelName = ModelName,
                EndpointUrl = EndpointUrl,
                ApiVersion = ApiVersion,
                DeploymentName = DeploymentName,
                ModelPath = ModelPath,
                SystemPrompt = SystemPrompt,
                ModerationEnabled = ModerationEnabled,
                EnableAdvancedAI = EnableAdvancedAI,
                IsLocalModel = IsLocalModel,
                IsActive = IsActive,
            };
        }

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
