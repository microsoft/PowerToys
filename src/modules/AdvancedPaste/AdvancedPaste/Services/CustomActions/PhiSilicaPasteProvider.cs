// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Text;
using PhiSilicaLanguageModel = Microsoft.Windows.AI.Text.LanguageModel;

namespace AdvancedPaste.Services.CustomActions;

public sealed class PhiSilicaPasteProvider : IPasteAIProvider
{
    private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
    {
        AIServiceType.PhiSilica,
    };

    public static PasteAIProviderRegistration Registration { get; } = new(SupportedTypes, config => new PhiSilicaPasteProvider(config));

    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static PhiSilicaLanguageModel _cachedModel;

    private readonly PasteAIConfig _config;

    public PhiSilicaPasteProvider(PasteAIConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            PhiSilicaLafHelper.TryUnlock();
            var readyState = PhiSilicaLanguageModel.GetReadyState();
            return Task.FromResult(readyState != AIFeatureReadyState.NotSupportedOnCurrentSystem);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<string> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var systemPrompt = request.SystemPrompt;
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new PasteActionException(
                    "System prompt is required for Phi Silica",
                    new ArgumentException("System prompt must be provided", nameof(request)));
            }

            var prompt = request.Prompt;
            var inputText = request.InputText;
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(inputText))
            {
                throw new PasteActionException(
                    "Prompt and input text are required",
                    new ArgumentException("Prompt and input text must be provided", nameof(request)));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var languageModel = await GetOrCreateModelAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report(0.1);

            var contentFilterOptions = new ContentFilterOptions();
            var context = languageModel.CreateContext(systemPrompt, contentFilterOptions);

            var userPrompt = $"""
                User instructions:
                {prompt}

                Text:
                {inputText}

                Output:
                """;

            if ((ulong)userPrompt.Length > languageModel.GetUsablePromptLength(context, userPrompt))
            {
                throw new PasteActionException(
                    "Prompt is too large for the Phi Silica model context",
                    new InvalidOperationException("Prompt exceeds usable prompt length"),
                    aiServiceMessage: "The input text is too large for on-device processing. Try with shorter text.");
            }

            var options = new LanguageModelOptions
            {
                ContentFilterOptions = contentFilterOptions,
            };

            var result = await languageModel.GenerateResponseAsync(context, userPrompt, options).AsTask(cancellationToken).ConfigureAwait(false);

            progress?.Report(0.8);

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

                throw new PasteActionException(
                    $"Phi Silica returned status: {result.Status}",
                    new InvalidOperationException($"LanguageModel response status: {result.Status}"),
                    aiServiceMessage: statusMessage);
            }

            var responseText = result.Text ?? string.Empty;
            request.Usage = AIServiceUsage.None;

            progress?.Report(1.0);

            return responseText;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PasteActionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PasteActionException(
                "Failed to generate response using Phi Silica",
                ex,
                aiServiceMessage: $"Error details: {ex.Message}");
        }
    }

    private static async Task<PhiSilicaLanguageModel> GetOrCreateModelAsync(CancellationToken cancellationToken)
    {
        if (_cachedModel is not null)
        {
            return _cachedModel;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedModel is not null)
            {
                return _cachedModel;
            }

            PhiSilicaLafHelper.TryUnlock();
            var readyState = PhiSilicaLanguageModel.GetReadyState();

            if (readyState is AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser)
            {
                throw new PasteActionException(
                    "Phi Silica is not supported on this device. A Copilot+ PC is required.",
                    new InvalidOperationException("Phi Silica requires a Copilot+ PC with an NPU."),
                    aiServiceMessage: "Phi Silica requires a Copilot+ PC with an NPU. For on-device AI on any Windows PC, consider using Foundry Local.");
            }

            if (readyState is AIFeatureReadyState.NotReady)
            {
                var ensureResult = await PhiSilicaLanguageModel.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (ensureResult.Status != AIFeatureReadyResultState.Success)
                {
                    throw new PasteActionException(
                        "Failed to prepare Phi Silica model",
                        ensureResult.ExtendedError,
                        aiServiceMessage: $"Model preparation failed (status: {ensureResult.Status})");
                }
            }

            if (PhiSilicaLanguageModel.GetReadyState() is not AIFeatureReadyState.Ready)
            {
                throw new PasteActionException(
                    "Phi Silica model is not ready",
                    new InvalidOperationException("Phi Silica model is not in Ready state after preparation."),
                    aiServiceMessage: "Phi Silica model is not available. Please ensure the model is downloaded and ready.");
            }

            _cachedModel = await PhiSilicaLanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
            return _cachedModel;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
