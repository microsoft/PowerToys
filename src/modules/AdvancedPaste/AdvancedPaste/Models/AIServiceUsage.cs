// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Models;

public record class AIServiceUsage(int PromptTokens, int CompletionTokens)
{
    public static AIServiceUsage None => new(PromptTokens: 0, CompletionTokens: 0);
}
