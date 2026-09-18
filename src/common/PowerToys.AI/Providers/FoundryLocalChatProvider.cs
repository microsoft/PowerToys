// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LanguageModelProvider;
using ManagedCommon;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Chat provider backed by a locally hosted Foundry Local model.
/// </summary>
public sealed class FoundryLocalChatProvider : IAIChatProvider
{
    private const int DefaultMaxOutputTokens = 2048;
    private const int MinOutputTokens = 256;
    private const long CacheSizeLimitBytes = 4 * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, IChatClient> CachedClients = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IDistributedCache ResponseCache = new MemoryDistributedCache(
        Options.Create(new MemoryDistributedCacheOptions { SizeLimit = CacheSizeLimitBytes }));

    private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = [AIServiceType.FoundryLocal];

    public static AIChatProviderRegistration Registration { get; } =
        new(SupportedTypes, config => new FoundryLocalChatProvider(config));

    private readonly AIProviderConfig _config;

    public FoundryLocalChatProvider(AIProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public async Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turns);

        var modelReference = _config.Model;
        if (string.IsNullOrWhiteSpace(modelReference))
        {
            throw new AIGenerationException("No Foundry Local model is selected. Choose a model in PowerToys AI settings.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        IChatClient? chatClient;
        try
        {
            // GetIChatClient blocks while it starts the service and loads the model — never run on the UI thread.
            chatClient = await Task.Run(
                () => FoundryLocalModelProvider.Instance.GetIChatClient(modelReference),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new AIGenerationException($"Unable to load the Foundry Local model '{modelReference}'.", ex);
        }

        if (chatClient is null)
        {
            throw new AIGenerationException($"Unable to load the Foundry Local model '{modelReference}'.");
        }

        var messages = BuildMessages(systemPrompt, turns);
        chatClient = Cached(modelReference, chatClient);

        var maxOutputTokens = _config.MaxOutputTokens ?? DefaultMaxOutputTokens;

        while (true)
        {
            try
            {
                var response = await chatClient.GetResponseAsync(messages, CreateChatOptions(maxOutputTokens), cancellationToken).ConfigureAwait(false);
                return GetResponseText(response);
            }
            catch (Exception ex) when (maxOutputTokens > MinOutputTokens && IsContextLengthError(ex))
            {
                maxOutputTokens /= 2;
                Logger.LogInfo($"Foundry Local rejected the request for exceeding the model's context window; retrying with MaxOutputTokens={maxOutputTokens}.");
            }
        }
    }

    private static IChatClient Cached(string modelReference, IChatClient inner) =>
        CachedClients.GetOrAdd(modelReference, _ => inner
            .AsBuilder()
            .UseDistributedCache(ResponseCache)
            .Build());

    private static bool IsContextLengthError(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("context length", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("context window", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ChatMessage> BuildMessages(string systemPrompt, IReadOnlyList<AIChatTurn> turns)
    {
        var messages = new List<ChatMessage>(turns.Count + 1);

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.AddRange(turns.Select(turn => new ChatMessage(turn.IsUser ? ChatRole.User : ChatRole.Assistant, turn.Text)));
        return messages;
    }

    private ChatOptions CreateChatOptions(int maxOutputTokens)
    {
        // ModelId is deliberately not set — the chat client was created for an already-resolved catalog name.
        var options = new ChatOptions
        {
            MaxOutputTokens = maxOutputTokens,
            ConversationId = null,
            Tools = null,
            ToolMode = ChatToolMode.None,
        };

        if (_config.ResponseFormat is not null)
        {
            options.ResponseFormat = _config.ResponseFormat;
        }

        return options;
    }

    private static string GetResponseText(ChatResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text;
        }

        var lastMessage = response.Messages?.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Text));
        return lastMessage?.Text ?? string.Empty;
    }
}
