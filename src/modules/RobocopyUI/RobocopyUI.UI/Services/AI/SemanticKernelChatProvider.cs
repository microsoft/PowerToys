// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Chat provider backed by Semantic Kernel connectors. Supports the same hosted services as
    /// Advanced Paste (OpenAI, Azure OpenAI, Mistral, Google, Azure AI Inference and Ollama).
    /// </summary>
    public sealed class SemanticKernelChatProvider : IAIChatProvider
    {
        public static IReadOnlyCollection<AIServiceType> SupportedTypes { get; } = new[]
        {
            AIServiceType.OpenAI,
            AIServiceType.AzureOpenAI,
            AIServiceType.Mistral,
            AIServiceType.Google,
            AIServiceType.AzureAIInference,
            AIServiceType.Ollama,
        };

        private readonly AIProviderConfig _config;
        private readonly AIServiceType _serviceType;

        public SemanticKernelChatProvider(AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
            _serviceType = config.ProviderType == AIServiceType.Unknown ? AIServiceType.OpenAI : config.ProviderType;
        }

        public async Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AIChatTurn> turns, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turns);

            var kernel = CreateKernel();
            var chatService = ResolveChatService(kernel);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);

            foreach (var turn in turns)
            {
                if (turn.IsUser)
                {
                    chatHistory.AddUserMessage(turn.Text);
                }
                else
                {
                    chatHistory.AddAssistantMessage(turn.Text);
                }
            }

            var response = await chatService.GetChatMessageContentAsync(chatHistory, CreateExecutionSettings(), kernel, cancellationToken);
            return response.Content ?? string.Empty;
        }

        private IChatCompletionService ResolveChatService(Kernel kernel)
        {
            if (!string.IsNullOrWhiteSpace(_config.Model))
            {
                try
                {
                    return kernel.GetRequiredService<IChatCompletionService>(_config.Model);
                }
                catch (Exception)
                {
                    // The connector may not have registered the service under the model id; fall back below.
                }
            }

            return kernel.GetRequiredService<IChatCompletionService>();
        }

        private Kernel CreateKernel()
        {
            var kernelBuilder = Kernel.CreateBuilder();
            var endpoint = string.IsNullOrWhiteSpace(_config.Endpoint) ? null : _config.Endpoint.Trim();
            var apiKey = _config.ApiKey?.Trim() ?? string.Empty;

            if (RequiresApiKey(_serviceType) && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AIGenerationException($"An API key is required for {_serviceType} but none was configured.");
            }

            var model = _config.Model;
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new AIGenerationException($"No model is configured for {_serviceType}. Select a model in PowerToys AI settings.");
            }

            switch (_serviceType)
            {
                case AIServiceType.OpenAI:
                    kernelBuilder.AddOpenAIChatCompletion(model, apiKey, serviceId: model);
                    break;
                case AIServiceType.AzureOpenAI:
                    var deploymentName = string.IsNullOrWhiteSpace(_config.DeploymentName) ? model : _config.DeploymentName;
                    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, RequireEndpoint(endpoint), apiKey, serviceId: model);
                    break;
                case AIServiceType.Mistral:
                    kernelBuilder.AddMistralChatCompletion(model, apiKey: apiKey);
                    break;
                case AIServiceType.Google:
                    kernelBuilder.AddGoogleAIGeminiChatCompletion(model, apiKey: apiKey);
                    break;
                case AIServiceType.AzureAIInference:
                    kernelBuilder.AddAzureAIInferenceChatCompletion(model, apiKey: apiKey, endpoint: new Uri(RequireEndpoint(endpoint)));
                    break;
                case AIServiceType.Ollama:
                    kernelBuilder.AddOllamaChatCompletion(model, endpoint: new Uri(RequireEndpoint(endpoint)));
                    break;
                default:
                    throw new AIGenerationException($"Provider '{_serviceType}' is not supported.");
            }

            return kernelBuilder.Build();
        }

        private PromptExecutionSettings CreateExecutionSettings()
        {
            return _serviceType switch
            {
                // Model-specific tuning properties are deliberately omitted: models accept different
                // value sets and a hardcoded value makes requests fail on models that don't support it.
                AIServiceType.OpenAI or AIServiceType.AzureOpenAI => new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = null,
                },
                _ => new PromptExecutionSettings(),
            };
        }

        private static bool RequiresApiKey(AIServiceType serviceType) => serviceType != AIServiceType.Ollama;

        private string RequireEndpoint(string? endpoint)
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }

            throw new AIGenerationException($"An endpoint is required for {_serviceType} but none was configured.");
        }
    }
}
