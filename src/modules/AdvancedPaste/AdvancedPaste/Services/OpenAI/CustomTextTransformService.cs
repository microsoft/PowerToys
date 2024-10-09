// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Azure;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace AdvancedPaste.Services.OpenAI;

public sealed class CustomTextTransformService(IAICredentialsProvider aiCredentialsProvider) : ICustomTextTransformService
{
    private const string ModelName = "gpt-3.5-turbo-instruct";

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;

    private async Task<Completions> GetAICompletionAsync(string systemInstructions, string userMessage)
    {
        OpenAIClient azureAIClient = new(_aiCredentialsProvider.Key);

        var response = await azureAIClient.GetCompletionsAsync(
            new()
            {
                DeploymentName = ModelName,
                Prompts =
                {
                    systemInstructions + "\n\n" + userMessage,
                },
                Temperature = 0.01F,
                MaxTokens = 2000,
            });

        if (response.Value.Choices[0].FinishReason == "length")
        {
            Logger.LogDebug("Cut off due to length constraints");
        }

        return response;
    }

    public async Task<string> TransformStringAsync(string inputInstructions, string inputString)
    {
        if (string.IsNullOrWhiteSpace(inputInstructions))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(inputString))
        {
            Logger.LogWarning("Clipboard has no usable text data");
            return string.Empty;
        }

        string systemInstructions =
$@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
Do not output anything else besides the reformatted clipboard content.";

        string userMessage =
$@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

        try
        {
            var reponse = await GetAICompletionAsync(systemInstructions, userMessage);
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomFormatEvent(reponse.Usage.PromptTokens, reponse.Usage.CompletionTokens, ModelName));
            return reponse.Choices[0].Text;
        }
        catch (Exception ex)
        {
            Logger.LogError($"{nameof(GetAICompletionAsync)} failed", ex);
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(ex.Message));

            throw new PasteActionException(ErrorHelpers.TranslateErrorText((ex as RequestFailedException)?.Status ?? -1), ex);
        }
    }
}
