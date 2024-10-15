// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry;

[EventData]
public class AdvancedPasteSemanticKernelFormatEvent(int promptTokens, int completionTokens, string modelName, List<string> usedActionChain) : EventBase, IEvent
{
    public int PromptTokens { get; set; } = promptTokens;

    public int CompletionTokens { get; set; } = completionTokens;

    public string ModelName { get; set; } = modelName;

    public List<string> UsedActionChain { get; set; } = usedActionChain;

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
