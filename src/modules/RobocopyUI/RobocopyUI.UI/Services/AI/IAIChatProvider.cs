// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// A chat completion backend. Mirrors the provider abstraction used by Advanced Paste so the
    /// same PowerToys AI provider configuration can drive both modules.
    /// </summary>
    public interface IAIChatProvider
    {
        Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken);
    }
}
