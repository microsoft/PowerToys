// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Amazon;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class SemanticKernelPasteProvider : IPasteAIProvider
    {
        private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
        {
            AIServiceType.OpenAI,
            AIServiceType.AzureOpenAI,
            AIServiceType.Mistral,
            AIServiceType.Google,
            AIServiceType.HuggingFace,
            AIServiceType.AzureAIInference,
            AIServiceType.Ollama,
            AIServiceType.Anthropic,
            AIServiceType.AmazonBedrock,
        };

        public static PasteAIProviderRegistration Registration { get; } = new(SupportedTypes, config => new SemanticKernelPasteProvider(config));

        private readonly PasteAIConfig _config;
        private readonly AIServiceType _serviceType;

        public SemanticKernelPasteProvider(PasteAIConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
            _serviceType = config.ProviderType;
            if (_serviceType == AIServiceType.Unknown)
            {
                _serviceType = AIServiceType.OpenAI;
                _config.ProviderType = _serviceType;
            }
        }

        public IReadOnlyCollection<AIServiceType> SupportedServiceTypes => SupportedTypes;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public async Task<string> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(request);

            var systemPrompt = request.SystemPrompt;
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new ArgumentException("System prompt must be provided", nameof(request));
            }

            var prompt = request.Prompt;
            var inputText = request.InputText;
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(inputText))
            {
                throw new ArgumentException("Prompt and input text must be provided", nameof(request));
            }

            var userMessageContent = $"""
                User instructions:
                {prompt}

                Clipboard Content:
                {inputText}

                Output:
                """;

            var executionSettings = CreateExecutionSettings();
            var kernel = CreateKernel();
            var modelId = _config.Model;

            IChatCompletionService chatService;
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                try
                {
                    chatService = kernel.GetRequiredService<IChatCompletionService>(modelId);
                }
                catch (Exception)
                {
                    chatService = kernel.GetRequiredService<IChatCompletionService>();
                }
            }
            else
            {
                chatService = kernel.GetRequiredService<IChatCompletionService>();
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessageContent);

            var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken);
            chatHistory.Add(response);

            request.Usage = AIServiceUsageHelper.GetOpenAIServiceUsage(response);
            return response.Content;
        }

        private Kernel CreateKernel()
        {
            var kernelBuilder = Kernel.CreateBuilder();
            var endpoint = string.IsNullOrWhiteSpace(_config.Endpoint) ? null : _config.Endpoint.Trim();
            var apiKey = _config.ApiKey?.Trim() ?? string.Empty;

            if (RequiresApiKey(_serviceType) && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException($"API key is required for {_serviceType} but was not provided.");
            }

            switch (_serviceType)
            {
                case AIServiceType.OpenAI:
                    kernelBuilder.AddOpenAIChatCompletion(_config.Model, apiKey, serviceId: _config.Model);
                    break;
                case AIServiceType.AzureOpenAI:
                    var deploymentName = string.IsNullOrWhiteSpace(_config.DeploymentName) ? _config.Model : _config.DeploymentName;
                    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, RequireEndpoint(endpoint, _serviceType), apiKey, serviceId: _config.Model);
                    break;
                case AIServiceType.Mistral:
                    kernelBuilder.AddMistralChatCompletion(_config.Model, apiKey: apiKey);
                    break;
                case AIServiceType.Google:
                    kernelBuilder.AddGoogleAIGeminiChatCompletion(_config.Model, apiKey: apiKey);
                    break;
                case AIServiceType.HuggingFace:
                    kernelBuilder.AddHuggingFaceChatCompletion(_config.Model, apiKey: apiKey);
                    break;
                case AIServiceType.AzureAIInference:
                    kernelBuilder.AddAzureAIInferenceChatCompletion(_config.Model, apiKey: apiKey, endpoint: new Uri(endpoint));
                    break;
                case AIServiceType.Ollama:
                    kernelBuilder.AddOllamaChatCompletion(_config.Model, endpoint: new Uri(endpoint));
                    break;
                case AIServiceType.Anthropic:
                    kernelBuilder.AddBedrockChatCompletionService(_config.Model);
                    break;
                case AIServiceType.AmazonBedrock:
                    kernelBuilder.AddBedrockChatCompletionService(_config.Model);
                    break;

                default:
                    throw new NotSupportedException($"Provider '{_config.ProviderType}' is not supported by {nameof(SemanticKernelPasteProvider)}");
            }

            return kernelBuilder.Build();
        }

        private PromptExecutionSettings CreateExecutionSettings()
        {
            return _serviceType switch
            {
                AIServiceType.OpenAI or AIServiceType.AzureOpenAI => new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.01,
                    MaxTokens = 2000,
                    FunctionChoiceBehavior = null,
                },
                _ => new PromptExecutionSettings(),
            };
        }

        private static bool RequiresApiKey(AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.Ollama => false,
                AIServiceType.Anthropic => false,
                AIServiceType.AmazonBedrock => false,
                _ => true,
            };
        }

        private static string RequireEndpoint(string endpoint, AIServiceType serviceType)
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }

            throw new InvalidOperationException($"Endpoint is required for {serviceType} but was not provided.");
        }
    }
}
