// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// A chat completion backend used by PowerToys modules that need AI text generation.
/// </summary>
public interface IAIChatProvider
{
    Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken);
}
