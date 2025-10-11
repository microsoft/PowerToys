// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class PasteAIRequest
    {
        public string Prompt { get; init; }

        public string InputText { get; init; }

        public string SystemPrompt { get; init; }

        public AIServiceUsage Usage { get; set; } = AIServiceUsage.None;
    }
}
