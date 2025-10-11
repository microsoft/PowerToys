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
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class SemanticKernelPasteProvider : IPasteAIProvider
    {
        private static readonly IReadOnlyCollection<AIServiceType> SupportedTypes = new[]
        {
            AIServiceType.OpenAI,
            AIServiceType.AzureOpenAI,
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

            switch (_serviceType)
            {
                case AIServiceType.OpenAI:
                    kernelBuilder.AddOpenAIChatCompletion(_config.Model, _config.ApiKey, serviceId: _config.Model);
                    break;

                case AIServiceType.AzureOpenAI:
                    var deploymentName = string.IsNullOrWhiteSpace(_config.DeploymentName) ? _config.Model : _config.DeploymentName;
                    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, _config.Endpoint, _config.ApiKey, serviceId: _config.Model);
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
    }
}
