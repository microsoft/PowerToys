// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Stores provider-specific configuration overrides so each AI service can keep distinct settings.
    /// </summary>
    public class AIProviderConfigurationSnapshot
    {
        [JsonPropertyName("model-name")]
        public string ModelName { get; set; } = string.Empty;

        [JsonPropertyName("endpoint-url")]
        public string EndpointUrl { get; set; } = string.Empty;

        [JsonPropertyName("api-version")]
        public string ApiVersion { get; set; } = string.Empty;

        [JsonPropertyName("deployment-name")]
        public string DeploymentName { get; set; } = string.Empty;

        [JsonPropertyName("model-path")]
        public string ModelPath { get; set; } = string.Empty;

        [JsonPropertyName("system-prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("moderation-enabled")]
        public bool ModerationEnabled { get; set; } = true;
    }
}
