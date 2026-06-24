// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using LanguageModelProvider;
using Microsoft.Extensions.AI;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services.CustomActions;

public sealed class FoundryLocalPasteProvider : IPasteAIProvider
{
    private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
    {
        AIServiceType.FoundryLocal,
    };

    public static PasteAIProviderRegistration Registration { get; } = new(SupportedTypes, config => new FoundryLocalPasteProvider(config));

    private static readonly FoundryLocalModelProvider _modelProvider = FoundryLocalModelProvider.Instance;

    private readonly PasteAIConfig _config;

    public FoundryLocalPasteProvider(PasteAIConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FoundryLocalModelProvider.Instance.IsAvailable().ConfigureAwait(false);
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
                    "System prompt is required for Foundry Local",
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

            var modelReference = _config?.Model;
            if (string.IsNullOrWhiteSpace(modelReference))
            {
                throw new PasteActionException(
                    "No Foundry Local model selected",
                    new InvalidOperationException("Model identifier is required"),
                    aiServiceMessage: "Please select a model in the AI provider settings.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            IChatClient chatClient;
            try
            {
                chatClient = _modelProvider.GetIChatClient(modelReference);
            }
            catch (InvalidOperationException ex)
            {
                // GetIChatClient throws InvalidOperationException for user-facing errors
                var errorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("FoundryLocal_UnableToLoadModel"), modelReference);
                throw new PasteActionException(
                    errorMessage,
                    ex,
                    aiServiceMessage: ex.Message);
            }

            var userMessageContent = $"""
                User instructions:
                {prompt}

                Text:
                {inputText}

                Output:
                """;

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userMessageContent),
            };

            var chatOptions = CreateChatOptions(_config?.SystemPrompt, modelReference);

            progress?.Report(0.1);

            var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken).ConfigureAwait(false);

            progress?.Report(0.8);

            var responseText = GetResponseText(response);
            request.Usage = ToUsage(response.Usage);

            progress?.Report(1.0);

            return responseText ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Let cancellation exceptions pass through unchanged
            throw;
        }
        catch (PasteActionException)
        {
            // Let our custom exceptions pass through unchanged
            throw;
        }
        catch (Exception ex)
        {
            // Wrap any other exceptions with context
            var modelInfo = !string.IsNullOrWhiteSpace(_config?.Model) ? $" (Model: {_config.Model})" : string.Empty;
            throw new PasteActionException(
                $"Failed to generate response using Foundry Local{modelInfo}",
                ex,
                aiServiceMessage: $"Error details: {ex.Message}");
        }
    }

    private static ChatOptions CreateChatOptions(string systemPrompt, string modelReference)
    {
        var options = new ChatOptions
        {
            ModelId = modelReference,
            MaxOutputTokens = 2048,
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            options.Instructions = systemPrompt;
        }

        return options;
    }

    private static string GetResponseText(ChatResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text;
        }

        if (response.Messages is { Count: > 0 })
        {
            var lastMessage = response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text));
            if (!string.IsNullOrWhiteSpace(lastMessage?.Text))
            {
                return lastMessage.Text;
            }
        }

        return string.Empty;
    }

    private static AIServiceUsage ToUsage(UsageDetails usageDetails)
    {
        if (usageDetails is null)
        {
            return AIServiceUsage.None;
        }

        int promptTokens = (int)(usageDetails.InputTokenCount ?? 0);
        int completionTokens = (int)(usageDetails.OutputTokenCount ?? 0);

        if (promptTokens == 0 && completionTokens == 0)
        {
            return AIServiceUsage.None;
        }

        return new AIServiceUsage(promptTokens, completionTokens);
    }
}
