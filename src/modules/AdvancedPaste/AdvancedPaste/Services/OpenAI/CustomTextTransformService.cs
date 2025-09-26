// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ClientModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using OpenAI;
using OpenAI.Chat;

namespace AdvancedPaste.Services.OpenAI;

public sealed class CustomTextTransformService(IUserSettings userSettings, IAICredentialsProvider aiCredentialsProvider, IPromptModerationService promptModerationService) : ICustomTextTransformService
{
    private readonly IUserSettings _userSettings = userSettings;

    private string ModelName => string.IsNullOrWhiteSpace(_userSettings.CustomModelName) ? "gpt-3.5-turbo-instruct" : _userSettings.CustomModelName;

    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;
    private readonly IPromptModerationService _promptModerationService = promptModerationService;

    private async Task<ChatCompletion> GetAICompletionAsync(string systemInstructions, string userMessage, CancellationToken cancellationToken)
    {
        var fullPrompt = systemInstructions + "\n\n" + userMessage;
        await _promptModerationService.ValidateAsync(fullPrompt, cancellationToken);

        OpenAIClientOptions clientOptions = new();
        if (!string.IsNullOrWhiteSpace(_userSettings.CustomEndpoint))
        {
            if (!Uri.TryCreate(_userSettings.CustomEndpoint, UriKind.Absolute, out var endpoint))
            {
                throw new ArgumentException($"Invalid custom endpoint URL: '{_userSettings.CustomEndpoint}'. Please ensure the URL includes the protocol (e.g., https://your-server.com/api) and is properly formatted.");
            }

            clientOptions.Endpoint = endpoint;
        }

        OpenAIClient openAIClient = new(new ApiKeyCredential(_aiCredentialsProvider.Key), clientOptions);

        var response = await openAIClient.GetChatClient(ModelName).CompleteChatAsync(
            [
                new SystemChatMessage(systemInstructions),
                new UserChatMessage(userMessage)
            ],
            new()
            {
                Temperature = 0.01F,
                MaxOutputTokenCount = 2000,
            },
            cancellationToken);

        if (response.Value.FinishReason == ChatFinishReason.Length)
        {
            Logger.LogDebug("Cut off due to length constraints");
        }

        return response.Value;
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
            AdvancedPasteGenerateCustomFormatEvent telemetryEvent = new(usage.InputTokenCount, usage.OutputTokenCount, ModelName);
            PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
            var logEvent = new AIServiceFormatEvent(telemetryEvent);

            Logger.LogDebug($"{nameof(TransformTextAsync)} complete; {logEvent.ToJsonString()}");

            return response.Content[0].Text;
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
                throw new PasteActionException(ErrorHelpers.TranslateErrorText((ex as ClientResultException)?.Status ?? -1), ex);
            }
        }
    }
}
