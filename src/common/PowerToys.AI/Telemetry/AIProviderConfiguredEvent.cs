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
/// Generic scaffold for AI provider configuration or active-provider changes.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AIProviderConfiguredEvent : EventBase, IEvent
{
    public string ModuleName { get; set; } = string.Empty;

    public string ProviderType { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public bool IsLocal { get; set; }

    public string Action { get; set; } = string.Empty;

    public AIProviderConfiguredEvent()
    {
    }

    public AIProviderConfiguredEvent(
        string moduleName,
        AIServiceType providerType,
        string providerId,
        string action,
        bool isLocal = false)
    {
        ModuleName = moduleName;
        ProviderType = providerType.ToString();
        ProviderId = providerId ?? string.Empty;
        Action = action ?? string.Empty;
        IsLocal = isLocal;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
