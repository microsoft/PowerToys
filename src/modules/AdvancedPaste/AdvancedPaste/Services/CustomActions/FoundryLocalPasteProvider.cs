// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using LanguageModelProvider;
using Microsoft.Extensions.AI;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions;

public sealed class FoundryLocalPasteProvider : IPasteAIProvider
{
    private static readonly LanguageModelService LanguageModels = LanguageModelService.CreateDefault();

    private readonly PasteAIConfig _config;

    public FoundryLocalPasteProvider(PasteAIConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string ProviderName => AIServiceType.FoundryLocal.ToNormalizedKey();

    public string DisplayName => string.IsNullOrWhiteSpace(_config?.Model) ? "Foundry Local" : _config.Model;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await FoundryLocalModelProvider.Instance.IsAvailable().ConfigureAwait(false);
    }

    public async Task<PasteAIProviderResult> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ChatHistory is null)
        {
            throw new ArgumentException("Chat history must be provided", nameof(request));
        }

        var modelReference = request.ModelId ?? _config?.Model;
        if (string.IsNullOrWhiteSpace(modelReference))
        {
            throw new InvalidOperationException("Foundry Local requires a model identifier (for example, 'fl://model-name').");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var chatClient = LanguageModels.GetClient(modelReference);
        if (chatClient is null)
        {
            throw new InvalidOperationException($"Unable to resolve Foundry Local client for '{modelReference}'. Ensure the model is downloaded.");
        }

        var chatMessages = ConvertToChatMessages(request.ChatHistory);
        var chatOptions = CreateChatOptions(request.ExecutionSettings ?? _config?.ExecutionSettings, _config?.SystemPrompt, modelReference);

        progress?.Report(0.1);

        var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken).ConfigureAwait(false);

        progress?.Report(0.8);

        var responseText = GetResponseText(response);
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            request.ChatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, responseText));
        }

        var usage = ToUsage(response.Usage);

        progress?.Report(1.0);

        return new PasteAIProviderResult(responseText ?? string.Empty, usage);
    }

    private static IReadOnlyList<ChatMessage> ConvertToChatMessages(ChatHistory history)
    {
        List<ChatMessage> messages = new(history.Count);

        foreach (var item in history)
        {
            if (item is null)
            {
                continue;
            }

            var text = item.Content;

            if (string.IsNullOrWhiteSpace(text) && item.Items is not null)
            {
                var itemTexts = item.Items
                    .Select(i => i?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                text = string.Join(Environment.NewLine, itemTexts);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            messages.Add(new ChatMessage(MapRole(item.Role), text));
        }

        return messages;
    }

    private static ChatOptions CreateChatOptions(PromptExecutionSettings executionSettings, string systemPrompt, string modelReference)
    {
        var options = new ChatOptions
        {
            ModelId = modelReference,
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            options.Instructions = systemPrompt;
        }

        if (!string.IsNullOrWhiteSpace(PromptExecutionSettings.DefaultServiceId))
        {
            options.ConversationId = PromptExecutionSettings.DefaultServiceId;
        }

        return options;
    }

    private static ChatRole MapRole(AuthorRole role)
    {
        if (role == AuthorRole.Assistant)
        {
            return ChatRole.Assistant;
        }

        if (role == AuthorRole.Tool)
        {
            return ChatRole.Tool;
        }

        if (role == AuthorRole.System || role == AuthorRole.Developer)
        {
            return ChatRole.System;
        }

        return ChatRole.User;
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
