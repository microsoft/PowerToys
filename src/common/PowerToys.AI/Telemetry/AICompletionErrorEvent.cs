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
/// Generic scaffold for AI completion failures. Modules set <see cref="ModuleName"/> when emitting.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AICompletionErrorEvent : EventBase, IEvent
{
    public string ModuleName { get; set; } = string.Empty;

    public string ProviderType { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string Error { get; set; } = string.Empty;

    public AICompletionErrorEvent()
    {
    }

    public AICompletionErrorEvent(
        string moduleName,
        AIServiceType providerType,
        string modelName,
        int statusCode,
        string error,
        string providerId = "")
    {
        ModuleName = moduleName;
        ProviderType = providerType.ToString();
        ModelName = modelName ?? string.Empty;
        ProviderId = providerId ?? string.Empty;
        StatusCode = statusCode;
        Error = error ?? string.Empty;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
