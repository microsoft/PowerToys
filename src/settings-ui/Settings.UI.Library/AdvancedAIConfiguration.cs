// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Configuration for Advanced AI features (general AI transformations like smart paste, content understanding)
    /// </summary>
    public class AdvancedAIConfiguration : INotifyPropertyChanged
    {
        private string _serviceType = "OpenAI";
        private string _modelName = "gpt-4";
        private string _endpointUrl = string.Empty;
        private string _apiVersion = string.Empty;
        private string _deploymentName = string.Empty;
        private string _modelPath = string.Empty;
        private bool _useSharedCredentials = true;
        private string _systemPrompt = string.Empty;

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
