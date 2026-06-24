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
    /// Gets or sets the AI provider type (e.g., OpenAI, AzureOpenAI, Google).
    /// </summary>
    public string ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the configured model name.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the advanced AI pipeline was used.
    /// </summary>
    public bool IsAdvanced { get; set; }

    /// <summary>
    /// Gets or sets the total duration in milliseconds, or -1 if unavailable.
    /// </summary>
    public int DurationMs { get; set; }

    public AdvancedPasteEndpointUsageEvent(AIServiceType providerType, string modelName, bool isAdvanced, int durationMs = -1)
    {
        ProviderType = providerType.ToString();
        ModelName = modelName;
        IsAdvanced = isAdvanced;
        DurationMs = durationMs;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
