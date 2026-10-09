// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.Json;

using AdvancedPaste.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace AdvancedPaste.UnitTests.Utils;

internal sealed class AdvancedPasteEventListener : EventListener
{
    private readonly List<AdvancedPasteGenerateCustomFormatEvent> _customFormatEvents = [];
    private readonly List<AdvancedPasteSemanticKernelFormatEvent> _semanticKernelEvents = [];

    public IReadOnlyList<AdvancedPasteGenerateCustomFormatEvent> CustomFormatEvents => _customFormatEvents;

    public IReadOnlyList<AdvancedPasteSemanticKernelFormatEvent> SemanticKernelEvents => _semanticKernelEvents;

    public int CustomFormatTokens => _customFormatEvents.Sum(e => e.PromptTokens + e.CompletionTokens);

    public int SemanticKernelTokens => _semanticKernelEvents.Sum(e => e.PromptTokens + e.CompletionTokens);

    public int TotalTokens => CustomFormatTokens + SemanticKernelTokens;

    internal AdvancedPasteEventListener()
    {
        EnableEvents(PowerToysTelemetry.Log, EventLevel.LogAlways);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != PowerToysTelemetry.Log.Name)
        {
            return;
        }

        var payloadDict = eventData.PayloadNames
                                   .Zip(eventData.Payload)
                                   .ToDictionary(tuple => tuple.First, tuple => tuple.Second);

        bool AddToListIfKeyExists<T>(string key, List<T> list)
        {
            if (payloadDict.ContainsKey(key))
            {
                var payloadJson = JsonSerializer.Serialize(payloadDict);
                list.Add(JsonSerializer.Deserialize<T>(payloadJson));
                return true;
            }

            return false;
        }

        if (!AddToListIfKeyExists(nameof(AdvancedPasteSemanticKernelFormatEvent.ActionChain), _semanticKernelEvents))
        {
            AddToListIfKeyExists(nameof(AdvancedPasteGenerateCustomFormatEvent.PromptTokens), _customFormatEvents);
        }
    }
}
