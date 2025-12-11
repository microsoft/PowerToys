// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Linq;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AdvancedPasteSemanticKernelFormatEvent(bool cacheUsed, bool isSavedQuery, int promptTokens, int completionTokens, string modelName, string providerType, string actionChain) : EventBase, IEvent
{
    public static string FormatActionChain(IEnumerable<ActionChainItem> actionChain) => FormatActionChain(actionChain.Select(item => item.Format));

    public static string FormatActionChain(IEnumerable<PasteFormats> actionChain) => string.Join(", ", actionChain);

    public bool IsSavedQuery { get; set; } = isSavedQuery;

    public bool CacheUsed { get; set; } = cacheUsed;

    public int PromptTokens { get; set; } = promptTokens;

    public int CompletionTokens { get; set; } = completionTokens;

    public string ModelName { get; set; } = modelName;

    public string ProviderType { get; set; } = providerType;

    /// <summary>
    /// Gets or sets a comma-separated list of paste formats used - in the same order they were executed.
    /// Conceptually an array but formatted this way to work around https://github.com/dotnet/runtime/issues/10428
    /// </summary>
    public string ActionChain { get; set; } = actionChain;

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
