// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Chat provider for a user-supplied local model file (ONNX / ML). Mirrors Advanced Paste's
    /// <c>LocalModelPasteProvider</c>, which is likewise a placeholder: no inference runtime is wired
    /// up for these service types yet.
    /// </summary>
    /// <remarks>
    /// The provider is registered rather than omitted so that selecting an ONNX/ML provider reports a
    /// clear reason instead of the generic "not supported by Robocopy UI" error. It deliberately
    /// fails instead of echoing its input the way the Advanced Paste placeholder does: this module
    /// parses the reply as a Robocopy plan, so returning the prompt back would surface as an opaque
    /// parse failure after a pointless round trip. Replace the body with real inference driven by
    /// <see cref="AIProviderConfig.ModelPath"/> when a runtime is available.
    /// </remarks>
    public sealed class LocalModelChatProvider : IAIChatProvider
    {
        public static IReadOnlyCollection<AIServiceType> SupportedTypes { get; } = new[]
        {
            AIServiceType.Onnx,
            AIServiceType.ML,
        };

        private readonly AIProviderConfig _config;

        public LocalModelChatProvider(AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
        }

        public Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turns);

            cancellationToken.ThrowIfCancellationRequested();

            throw new AIGenerationException(
                $"Local model provider '{_config.ProviderType}' is not supported yet. Select a different AI provider in PowerToys AI settings, or use Foundry Local for on-device generation.");
        }
    }
}
