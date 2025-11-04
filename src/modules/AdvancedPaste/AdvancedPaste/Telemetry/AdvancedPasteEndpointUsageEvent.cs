// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AdvancedPasteEndpointUsageEvent : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets the AI provider type (e.g., OpenAI, AzureOpenAI, Anthropic).
    /// </summary>
    public string ProviderType { get; set; }

    public AdvancedPasteEndpointUsageEvent(AIServiceType providerType)
    {
        ProviderType = providerType.ToString();
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
