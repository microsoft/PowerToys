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

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Chat provider backed by a locally hosted Foundry Local model.
    /// </summary>
    public sealed class FoundryLocalChatProvider : IAIChatProvider
    {
        /// <summary>
        /// Output budget for the reply.
        /// </summary>
        /// <remarks>
        /// The expected answer is a single plan object - in practice well under 200 tokens even with a
        /// long explanation. This is a hard reservation, not a cap that only matters when hit: Foundry
        /// Local rejects the request outright when input + MaxOutputTokens exceeds the model's context
        /// window, so an oversized value fails 4k-context models such as
        /// deepseek-r1-distill-qwen-7b-qnn-npu before a single token is generated. 512 leaves ample
        /// headroom for the reply while keeping the total inside a 4096 token window. If even this does
        /// not fit, the shrink-and-retry loop in <see cref="CompleteAsync"/> lowers it further.
        /// </remarks>
        private const int MaxOutputTokens = 512;

        /// <summary>
        /// Floor for the shrink-and-retry loop. A plan object does not fit below this, so a model that
        /// still cannot accept the request has too small a context window for this prompt and should
        /// surface the runtime's own error rather than be retried into uselessness.
        /// </summary>
        private const int MinOutputTokens = 256;

        /// <summary>
        /// Fixed seed so that repeat runs of the same prompt against the same model produce the same
        /// plan. The value itself is arbitrary; only its stability matters.
        /// </summary>
        private const long DeterministicSeed = 20250916;

        /// <summary>
        /// Cuts the response off if the model starts a markdown fence or hallucinates another turn
        /// after the JSON object it was asked for.
        /// </summary>
        private static readonly IList<string> StopSequences = new[] { "```", "\n\nUser:", "\n\nRequest:" };

        /// <summary>
        /// The plan schema, sent so that a runtime capable of constrained decoding will honor it.
        /// </summary>
        /// <remarks>
        /// Measured against Foundry Local 0.10.3: the service accepts json_schema, json_object and even
        /// lark_grammar with HTTP 200 and then ignores all of them, returning ordinary prose. Guidance
        /// is only applied for user-specified response formats from foundry-local PR 1043 onward, so on
        /// this build the schema is inert, and the prompt plus the generator's parse-and-repair loop are
        /// what actually enforce the output shape. It is still sent because it costs nothing and starts
        /// working by itself once the service is updated, but do not drop the validation layer on the
        /// strength of it.
        /// </remarks>
        private static readonly ChatResponseFormat PlanResponseFormat = RobocopyPlanSchema.CreateResponseFormat();

        public static IReadOnlyCollection<AIServiceType> SupportedTypes { get; } = new[]
        {
            AIServiceType.FoundryLocal,
        };

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
                // GetIChatClient blocks while it starts the Foundry Local service and loads the model.
                // It must never run on the UI thread, otherwise the app hangs until the model is ready.
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

            // Foundry Local validates input + MaxOutputTokens against the model's context window and
            // rejects the whole request when it does not fit, so a model with a small window fails
            // before generating anything. The window is not exposed through the chat client, and it
            // varies widely across catalog models, so the budget is discovered by retrying with a
            // smaller one rather than assumed.
            var maxOutputTokens = MaxOutputTokens;

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

        /// <summary>
        /// Wraps a chat client so that an identical request is answered from memory instead of being
        /// re-inferred.
        /// </summary>
        /// <remarks>
        /// Generation on a local model costs seconds, and this feature re-asks the same question often:
        /// the repair loop, the destructive-plan review pass and users retrying a phrasing all resend
        /// requests that frequently match one already answered. The cache key covers the messages and
        /// the options, so a hit only happens on a genuinely identical request, and the zero temperature
        /// and fixed seed mean a fresh call was supposed to produce the same answer anyway.
        /// <para>
        /// The cache is per model, keyed by the resolved model reference, so switching models in
        /// settings cannot serve an answer generated by a different one. It is in-process and bounded by
        /// a size limit rather than persisted, because the value is in a single session's repeats and a
        /// stale answer surviving a restart would be harder to reason about than simply regenerating.
        /// </para>
        /// </remarks>
        private static IChatClient Cached(string modelReference, IChatClient inner) =>
            CachedClients.GetOrAdd(modelReference, _ => inner
                .AsBuilder()
                .UseDistributedCache(ResponseCache)
                .Build());

        private static readonly ConcurrentDictionary<string, IChatClient> CachedClients = new(StringComparer.OrdinalIgnoreCase);

        private static readonly IDistributedCache ResponseCache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions { SizeLimit = CacheSizeLimitBytes }));

        /// <summary>
        /// Upper bound on cached responses. Each entry is a serialized plan of a few hundred bytes, so
        /// this holds thousands of them while staying negligible against the model itself.
        /// </summary>
        private const long CacheSizeLimitBytes = 4 * 1024 * 1024;

        /// <summary>
        /// Detects the runtime's "exceeds the model's maximum context length" rejection, which arrives
        /// as a generic HTTP 500 with the detail only in the message text.
        /// </summary>
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

        private static ChatOptions CreateChatOptions(int maxOutputTokens) => new()
        {
            // ModelId is deliberately not set. The chat client was created for an already-resolved
            // catalog model name, and setting ModelId here would override it with the raw settings
            // value - which may be an alias the inference endpoint rejects with HTTP 404.
            MaxOutputTokens = maxOutputTokens,

            // This is a structured extraction task, not a creative one. Greedy decoding gives the
            // most reproducible answer a model can offer: zero temperature removes sampling
            // randomness, TopP/TopK collapse the candidate set to the single highest-probability
            // token, and a fixed seed pins any residual tie-breaking that the runtime still does.
            Temperature = 0,
            TopP = 1,
            TopK = 1,
            Seed = DeterministicSeed,

            // Repetition penalties are deliberately NOT set. Sending FrequencyPenalty to the Foundry
            // Local runtime makes small models degenerate into an endless run of '.' characters that
            // only stops at MaxOutputTokens, which reads as a hang. Leaving the properties null keeps
            // the penalties out of the request payload entirely; greedy decoding above already makes
            // the output deterministic without them.

            // A single JSON object is the whole expected answer. Stopping at the first markdown
            // fence or follow-on turn keeps trailing commentary out of the payload the parser sees.
            StopSequences = StopSequences,

            // Constrain output to the plan schema where the model supports it. Not all local models
            // honor this, so the generator still parses and validates defensively.
            ResponseFormat = PlanResponseFormat,

            // Each generation is a self-contained request. Leaving the conversation unthreaded keeps
            // the model from inheriting hidden server-side state between runs.
            ConversationId = null,

            // No tools: the model's only job is to emit the plan object, and letting it attempt tool
            // calls is another source of run-to-run variation.
            Tools = null,
            ToolMode = ChatToolMode.None,
        };

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
}
