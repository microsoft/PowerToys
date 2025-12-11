// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.SerializationContext;
using AdvancedPaste.Telemetry;

namespace AdvancedPaste.Helpers
{
    public class AIServiceFormatEvent
    {
        public AIServiceFormatEvent(AdvancedPasteSemanticKernelFormatEvent semanticKernelFormatEvent)
        {
            CacheUsed = semanticKernelFormatEvent.CacheUsed;
            IsSavedQuery = semanticKernelFormatEvent.IsSavedQuery;
            PromptTokens = semanticKernelFormatEvent.PromptTokens;
            CompletionTokens = semanticKernelFormatEvent.CompletionTokens;
            ModelName = semanticKernelFormatEvent.ModelName;
            ProviderType = semanticKernelFormatEvent.ProviderType;
            ActionChain = semanticKernelFormatEvent.ActionChain;
        }

        public AIServiceFormatEvent(AdvancedPasteGenerateCustomFormatEvent generateCustomFormatEvent)
        {
            PromptTokens = generateCustomFormatEvent.PromptTokens;
            CompletionTokens = generateCustomFormatEvent.CompletionTokens;
            ModelName = generateCustomFormatEvent.ModelName;
        }

        public bool IsSavedQuery { get; set; }

        public bool CacheUsed { get; set; }

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public string ModelName { get; set; }

        public string ProviderType { get; set; }

        public string ActionChain { get; set; }

        public string ToJsonString() => JsonSerializer.Serialize(this, SourceGenerationContext.Default.AIServiceFormatEvent);
    }
}
