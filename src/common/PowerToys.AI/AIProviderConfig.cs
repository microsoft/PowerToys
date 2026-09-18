// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.AI;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Runtime configuration for creating an <see cref="IAIChatProvider"/>.
/// </summary>
public sealed class AIProviderConfig
{
    public AIServiceType ProviderType { get; set; }

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string ModelPath { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public bool ModerationEnabled { get; set; }

    /// <summary>
    /// Optional response format for providers that support constrained decoding.
    /// </summary>
    public ChatResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Optional max output tokens. Providers may apply their own defaults when null.
    /// </summary>
    public int? MaxOutputTokens { get; set; }
}
