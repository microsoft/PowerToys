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
public sealed class AdvancedPasteCustomActionErrorEvent : EventBase, IEvent
{
    public AdvancedPasteCustomActionErrorEvent(AIServiceType providerType, string modelName, int statusCode, string error)
    {
        ProviderType = providerType.ToString();
        ModelName = modelName;
        StatusCode = statusCode;
        Error = error;
    }

    public string ProviderType { get; set; }

    public string ModelName { get; set; }

    public int StatusCode { get; set; }

    public string Error { get; set; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
