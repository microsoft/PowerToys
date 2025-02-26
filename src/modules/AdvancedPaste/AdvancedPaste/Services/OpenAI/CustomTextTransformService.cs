// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Telemetry;
using Azure;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace AdvancedPaste.Services.OpenAI;

public sealed class CustomTextTransformService(IAICredentialsProvider aiCredentialsProvider, IPromptModerationService promptModerationService) : ICustomTextTransformService
{
    private const string ModelName = "gpt-3.5-turbo-instruct";

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;
    private readonly IPromptModerationService _promptModerationService = promptModerationService;

    private async Task<Completions> GetAICompletionAsync(string systemInstructions, string userMessage, CancellationToken cancellationToken)
    {
        var fullPrompt = systemInstructions + "\n\n" + userMessage;

        await _promptModerationService.ValidateAsync(fullPrompt, cancellationToken);

        OpenAIClient azureAIClient = new(_aiCredentialsProvider.Key);

        var response = await azureAIClient.GetCompletionsAsync(
            new()
            {
                DeploymentName = ModelName,
                Prompts =
                {
                    fullPrompt,
                },
                Temperature = 0.01F,
                MaxTokens = 2000,
            },
            cancellationToken);

        if (response.Value.Choices[0].FinishReason == "length")
        {
            Logger.LogDebug("Cut off due to length constraints");
        }

        return response;
    }

    public async Task<string> TransformTextAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(inputText))
        {
            Logger.LogWarning("Clipboard has no usable text data");
            return string.Empty;
        }

        string systemInstructions =
$@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
Do not output anything else besides the reformatted clipboard content.";

        string userMessage =
$@"User instructions:
{prompt}

Clipboard Content:
{inputText}

Output:
";

        try
        {
            var response = await GetAICompletionAsync(systemInstructions, userMessage, cancellationToken);

            var usage = response.Usage;
            AdvancedPasteGenerateCustomFormatEvent telemetryEvent = new(usage.PromptTokens, usage.CompletionTokens, ModelName);
            PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
            var logEvent = new AIServiceFormatEvent(telemetryEvent);

            Logger.LogDebug($"{nameof(TransformTextAsync)} complete; {logEvent.ToJsonString()}");

            return response.Choices[0].Text;
        }
        catch (Exception ex)
        {
            Logger.LogError($"{nameof(TransformTextAsync)} failed", ex);

            AdvancedPasteGenerateCustomErrorEvent errorEvent = new(ex is PasteActionModeratedException ? PasteActionModeratedException.ErrorDescription : ex.Message);
            PowerToysTelemetry.Log.WriteEvent(errorEvent);

            if (ex is PasteActionException or OperationCanceledException)
            {
                throw;
            }
            else
            {
                throw new PasteActionException(ErrorHelpers.TranslateErrorText((ex as RequestFailedException)?.Status ?? -1), ex);
            }
        }
    }
}
