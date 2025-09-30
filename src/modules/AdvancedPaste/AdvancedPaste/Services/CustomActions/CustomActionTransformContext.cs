// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformContext
    {
        public string Prompt { get; init; }

        public string InputText { get; init; }

        public PasteAIConfig ProviderConfig { get; init; }

        public PromptExecutionSettings ExecutionSettings { get; init; }

        public string ModelId { get; init; }

        public Func<ChatMessageContent, AIServiceUsage> UsageExtractor { get; init; }

        public string SystemPrompt { get; init; }
    }
}
