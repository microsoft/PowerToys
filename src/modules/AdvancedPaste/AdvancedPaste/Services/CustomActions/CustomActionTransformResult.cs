// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformResult
    {
        public CustomActionTransformResult(string content, AIServiceUsage usage, ChatHistory chatHistory)
        {
            Content = content;
            Usage = usage;
            ChatHistory = chatHistory;
        }

        public string Content { get; }

        public AIServiceUsage Usage { get; }

        public ChatHistory ChatHistory { get; }
    }
}
