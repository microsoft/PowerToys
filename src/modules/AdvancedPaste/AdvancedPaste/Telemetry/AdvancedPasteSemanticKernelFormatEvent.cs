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
public class AdvancedPasteSemanticKernelFormatEvent(int promptTokens, int completionTokens, string modelName, string usedActionChain) : EventBase, IEvent
{
    public static string FormatActionChain(IEnumerable<PasteFormats> usedActionChain) => string.Join(", ", usedActionChain);

    public int PromptTokens { get; set; } = promptTokens;

    public int CompletionTokens { get; set; } = completionTokens;

    public string ModelName { get; set; } = modelName;

    /// <summary>
    /// Gets or sets a comma-separated list of paste formats used - in the same order they were executed.
    /// Conceptually an array of strings but formatted this way to work around https://github.com/dotnet/runtime/issues/10428
    /// </summary>
    public string UsedActionChain { get; set; } = usedActionChain;

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
