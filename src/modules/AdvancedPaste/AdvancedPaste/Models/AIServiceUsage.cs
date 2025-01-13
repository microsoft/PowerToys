// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Models;

public record class AIServiceUsage(int PromptTokens, int CompletionTokens)
{
    public static AIServiceUsage None => new(PromptTokens: 0, CompletionTokens: 0);

    public bool HasUsage => PromptTokens > 0 || CompletionTokens > 0;

    public static AIServiceUsage Add(AIServiceUsage first, AIServiceUsage second) =>
        new(first.PromptTokens + second.PromptTokens, first.CompletionTokens + second.CompletionTokens);

    public override string ToString() =>
        $"{nameof(PromptTokens)}: {PromptTokens}, {nameof(CompletionTokens)}: {CompletionTokens}";
}
