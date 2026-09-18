// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Placeholder for ONNX / ML local models. Not yet implemented.
/// </summary>
public sealed class LocalModelChatProvider : IAIChatProvider
{
    private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes =
    [
        AIServiceType.Onnx,
        AIServiceType.ML,
    ];

    public static AIChatProviderRegistration Registration { get; } =
        new(SupportedTypes, config => new LocalModelChatProvider(config));

    public LocalModelChatProvider(AIProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
    }

    public Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
    {
        throw new AIGenerationException(
            "ONNX and ML local models are not supported yet. Configure Foundry Local or Phi Silica for on-device AI.");
    }
}
