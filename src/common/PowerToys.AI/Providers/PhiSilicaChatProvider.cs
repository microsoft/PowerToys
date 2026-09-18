// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using PhiSilicaLanguageModel = Microsoft.Windows.AI.Text.LanguageModel;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Chat provider backed by on-device Phi Silica (Windows App SDK AI).
/// </summary>
public sealed class PhiSilicaChatProvider : IAIChatProvider
{
    private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = [AIServiceType.PhiSilica];

    public static AIChatProviderRegistration Registration { get; } =
        new(SupportedTypes, config => new PhiSilicaChatProvider(config));

    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static PhiSilicaLanguageModel? cachedModel;

    private readonly AIProviderConfig _config;

    public PhiSilicaChatProvider(AIProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public async Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turns);

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new AIGenerationException("System prompt is required for Phi Silica.");
        }

        if (turns.Count == 0 || string.IsNullOrWhiteSpace(turns[^1].Text))
        {
            throw new AIGenerationException("At least one user turn is required for Phi Silica.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var languageModel = await GetOrCreateModelAsync(cancellationToken).ConfigureAwait(false);
        var contentFilterOptions = new ContentFilterOptions();
        using var context = languageModel.CreateContext(systemPrompt, contentFilterOptions);

        var userPrompt = BuildUserPrompt(turns);

        if ((ulong)userPrompt.Length > languageModel.GetUsablePromptLength(context, userPrompt))
        {
            throw new AIGenerationException("The prompt is too large for the Phi Silica model context.");
        }

        var options = new LanguageModelOptions
        {
            ContentFilterOptions = contentFilterOptions,
        };

        var result = await languageModel.GenerateResponseAsync(context, userPrompt, options).AsTask(cancellationToken).ConfigureAwait(false);

        if (result.Status != LanguageModelResponseStatus.Complete)
        {
            var statusMessage = result.Status switch
            {
                LanguageModelResponseStatus.BlockedByPolicy => "Response was blocked by policy.",
                LanguageModelResponseStatus.PromptBlockedByContentModeration => "Prompt was blocked by content moderation.",
                LanguageModelResponseStatus.ResponseBlockedByContentModeration => "Response was blocked by content moderation.",
                LanguageModelResponseStatus.PromptLargerThanContext => "Prompt is too large for the model context.",
                _ => $"Unexpected status: {result.Status}",
            };

            throw new AIGenerationException($"Phi Silica returned status: {result.Status}. {statusMessage}");
        }

        return result.Text ?? string.Empty;
    }

    private static string BuildUserPrompt(IReadOnlyList<AIChatTurn> turns)
    {
        // Phi Silica context already holds the system prompt; concatenate remaining turns.
        var parts = new List<string>(turns.Count);
        foreach (var turn in turns)
        {
            parts.Add(turn.IsUser ? turn.Text : $"Assistant:\n{turn.Text}");
        }

        return string.Join("\n\n", parts);
    }

    private static async Task<PhiSilicaLanguageModel> GetOrCreateModelAsync(CancellationToken cancellationToken)
    {
        if (cachedModel is not null)
        {
            return cachedModel;
        }

        await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cachedModel is not null)
            {
                return cachedModel;
            }

            if (!PhiSilicaLafHelper.TryUnlock())
            {
                throw new AIGenerationException(
                    $"Phi Silica access is unavailable on this device. ({PhiSilicaLafHelper.LastUnlockStatus})");
            }

            var readyState = PhiSilicaLanguageModel.GetReadyState();

            if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser)
            {
                throw new AIGenerationException(
                    "Phi Silica requires a Copilot+ PC with an NPU. For on-device AI on any Windows PC, consider using Foundry Local.");
            }

            if (readyState is AIFeatureReadyState.NotReady)
            {
                var ensureResult = await PhiSilicaLanguageModel.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (ensureResult.Status != AIFeatureReadyResultState.Success)
                {
                    throw new AIGenerationException($"Failed to prepare Phi Silica model (status: {ensureResult.Status}).", ensureResult.ExtendedError);
                }
            }

            if (PhiSilicaLanguageModel.GetReadyState() is not AIFeatureReadyState.Ready)
            {
                throw new AIGenerationException("Phi Silica model is not available. Ensure the model is downloaded and ready.");
            }

            cachedModel = await PhiSilicaLanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
            return cachedModel;
        }
        finally
        {
            InitLock.Release();
        }
    }
}
