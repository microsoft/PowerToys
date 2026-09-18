// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerToys.AI.Telemetry;

/// <summary>
/// Generic scaffold for AI endpoint usage. Modules set <see cref="ModuleName"/> when emitting.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AIEndpointUsageEvent : EventBase, IEvent
{
    public string ModuleName { get; set; } = string.Empty;

    public string ProviderType { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public bool IsLocal { get; set; }

    public bool IsAdvanced { get; set; }

    public int DurationMs { get; set; } = -1;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public AIEndpointUsageEvent()
    {
    }

    public AIEndpointUsageEvent(
        string moduleName,
        AIServiceType providerType,
        string modelName,
        string providerId = "",
        bool isLocal = false,
        bool isAdvanced = false,
        int durationMs = -1,
        int promptTokens = 0,
        int completionTokens = 0)
    {
        ModuleName = moduleName;
        ProviderType = providerType.ToString();
        ModelName = modelName ?? string.Empty;
        ProviderId = providerId ?? string.Empty;
        IsLocal = isLocal;
        IsAdvanced = isAdvanced;
        DurationMs = durationMs;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
