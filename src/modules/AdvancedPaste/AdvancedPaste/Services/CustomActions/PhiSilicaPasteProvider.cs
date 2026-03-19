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

            var fullPrompt = $"""
                {systemPrompt}

                User instructions:
                {prompt}

                Text:
                {inputText}

                Output:
                """;

            var result = await languageModel.GenerateResponseAsync(fullPrompt).AsTask(cancellationToken).ConfigureAwait(false);

            progress?.Report(0.8);

            var responseText = result?.Text ?? string.Empty;
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

            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
            {
                throw new PasteActionException(
                    "Phi Silica is not supported on this device. A Copilot+ PC is required.",
                    new InvalidOperationException("Phi Silica requires a Copilot+ PC with an NPU."),
                    aiServiceMessage: "Phi Silica requires a Copilot+ PC with an NPU. For on-device AI on any Windows PC, consider using Foundry Local.");
            }

            if (readyState == AIFeatureReadyState.NotReady)
            {
                var ensureResult = await PhiSilicaLanguageModel.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false);
                if (ensureResult.ExtendedError is not null)
                {
                    throw new PasteActionException(
                        "Failed to prepare Phi Silica model",
                        ensureResult.ExtendedError,
                        aiServiceMessage: $"Model installation failed: {ensureResult.ExtendedError.Message}");
                }
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
