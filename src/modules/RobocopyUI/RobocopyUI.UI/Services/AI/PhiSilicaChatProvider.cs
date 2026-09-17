// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using PhiSilicaLanguageModel = Microsoft.Windows.AI.Text.LanguageModel;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Chat provider backed by the on-device Phi Silica model. Requires a Copilot+ PC with an NPU.
    /// </summary>
    public sealed class PhiSilicaChatProvider : IAIChatProvider
    {
        public static IReadOnlyCollection<AIServiceType> SupportedTypes { get; } = new[]
        {
            AIServiceType.PhiSilica,
        };

        private static readonly SemaphoreSlim InitLock = new(1, 1);
        private static PhiSilicaLanguageModel? _cachedModel;

        private readonly AIProviderConfig _config;

        public PhiSilicaChatProvider(AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
        }

        /// <summary>
        /// Reports whether the device can run Phi Silica at all, without paying the cost of
        /// downloading or loading the model.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                if (!PhiSilicaLafHelper.TryUnlock())
                {
                    return false;
                }

                var readyState = PhiSilicaLanguageModel.GetReadyState();
                return readyState is not (AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turns);

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new AIGenerationException("A system prompt is required for Phi Silica.");
            }

            if (turns.Count == 0)
            {
                throw new AIGenerationException("No request was provided to generate a command from.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var languageModel = await GetOrCreateModelAsync(cancellationToken).ConfigureAwait(false);

            var contentFilterOptions = new ContentFilterOptions();
            using var context = languageModel.CreateContext(systemPrompt, contentFilterOptions);

            var userPrompt = BuildPrompt(turns);

            // Phi Silica rejects an over-long prompt outright, so check before spending time on
            // generation and surface a message the user can act on.
            if ((ulong)userPrompt.Length > languageModel.GetUsablePromptLength(context, userPrompt))
            {
                throw new AIGenerationException("The request is too large for the on-device Phi Silica model. Try a shorter description.");
            }

            var options = new LanguageModelOptions
            {
                ContentFilterOptions = contentFilterOptions,
            };

            LanguageModelResponseResult result;
            try
            {
                result = await languageModel.GenerateResponseAsync(context, userPrompt, options).AsTask(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AIGenerationException($"Phi Silica failed to generate a response: {ex.Message}", ex);
            }

            if (result.Status != LanguageModelResponseStatus.Complete)
            {
                throw new AIGenerationException(DescribeStatus(result.Status));
            }

            return result.Text ?? string.Empty;
        }

        /// <summary>
        /// Flattens the conversation into a single prompt. Phi Silica's context carries the system
        /// prompt but exposes no multi-turn message list, so prior turns are replayed inline to keep
        /// follow-up refinements working the same way they do on the hosted providers.
        /// </summary>
        private static string BuildPrompt(IReadOnlyList<AIChatTurn> turns)
        {
            if (turns.Count == 1)
            {
                return $"""
                    Request:
                    {turns[0].Text}

                    Output:
                    """;
            }

            var builder = new StringBuilder();

            foreach (var turn in turns)
            {
                builder.Append(turn.IsUser ? "Request:" : "Previous answer:")
                       .Append('\n')
                       .Append(turn.Text)
                       .Append("\n\n");
            }

            builder.Append("Output:");
            return builder.ToString();
        }

        private static string DescribeStatus(LanguageModelResponseStatus status) => status switch
        {
            LanguageModelResponseStatus.BlockedByPolicy => "The response was blocked by policy.",
            LanguageModelResponseStatus.PromptBlockedByContentModeration => "The request was blocked by content moderation.",
            LanguageModelResponseStatus.ResponseBlockedByContentModeration => "The response was blocked by content moderation.",
            LanguageModelResponseStatus.PromptLargerThanContext => "The request is too large for the on-device model context.",
            _ => $"Phi Silica returned an unexpected status: {status}.",
        };

        private static async Task<PhiSilicaLanguageModel> GetOrCreateModelAsync(CancellationToken cancellationToken)
        {
            if (_cachedModel is not null)
            {
                return _cachedModel;
            }

            await InitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cachedModel is not null)
                {
                    return _cachedModel;
                }

                if (!PhiSilicaLafHelper.TryUnlock())
                {
                    throw new AIGenerationException($"Phi Silica access is unavailable on this device (status: {PhiSilicaLafHelper.LastUnlockStatus}).");
                }

                var readyState = PhiSilicaLanguageModel.GetReadyState();

                if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser)
                {
                    throw new AIGenerationException("Phi Silica requires a Copilot+ PC with an NPU. For on-device AI on any Windows PC, consider using Foundry Local.");
                }

                if (readyState is AIFeatureReadyState.NotReady)
                {
                    // The model is supported but not yet downloaded. This can take a while, which is
                    // why callers must invoke the provider off the UI thread.
                    var ensureResult = await PhiSilicaLanguageModel.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false);
                    if (ensureResult.Status != AIFeatureReadyResultState.Success)
                    {
                        throw new AIGenerationException($"Failed to prepare the Phi Silica model (status: {ensureResult.Status}).", ensureResult.ExtendedError);
                    }
                }

                if (PhiSilicaLanguageModel.GetReadyState() is not AIFeatureReadyState.Ready)
                {
                    throw new AIGenerationException("The Phi Silica model is not available. Ensure the model is downloaded and ready.");
                }

                _cachedModel = await PhiSilicaLanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
                return _cachedModel;
            }
            finally
            {
                InitLock.Release();
            }
        }
    }
}
