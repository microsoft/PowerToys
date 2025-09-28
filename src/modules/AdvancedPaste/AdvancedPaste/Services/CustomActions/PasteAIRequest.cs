// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class PasteAIRequest
    {
        public ChatHistory ChatHistory { get; set; }

        public PromptExecutionSettings ExecutionSettings { get; set; }

        public Func<Kernel> KernelFactory { get; set; }

        public string ModelId { get; set; }

        public Func<ChatMessageContent, AIServiceUsage> UsageExtractor { get; set; }
    }
}
