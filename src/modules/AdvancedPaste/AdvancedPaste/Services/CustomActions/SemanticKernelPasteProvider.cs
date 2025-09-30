// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class SemanticKernelPasteProvider : IPasteAIProvider
    {
        private readonly PasteAIConfig _config;
        private readonly string _providerType;

        public SemanticKernelPasteProvider(PasteAIConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
            _providerType = NormalizeProviderType(config.ProviderType);
            _config.ProviderType = _providerType;
        }

        public string ProviderName => _providerType;

        public string DisplayName => string.IsNullOrEmpty(_config?.Model) ? _providerType : _config.Model;

        public bool IsLocal => _config?.IsLocal ?? false;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public async Task<PasteAIProviderResult> ProcessPasteAsync(PasteAIRequest request, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.ChatHistory is null)
            {
                throw new ArgumentException("Chat history must be provided", nameof(request));
            }

            var executionSettings = request.ExecutionSettings ?? _config.ExecutionSettings ?? new PromptExecutionSettings();
            var kernel = CreateKernel(request);
            var modelId = request.ModelId ?? _config.Model;

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

            var response = await chatService.GetChatMessageContentAsync(request.ChatHistory, executionSettings, kernel, cancellationToken);

            request.ChatHistory.Add(response);

            var usageExtractor = request.UsageExtractor ?? _config.UsageExtractor;
            AIServiceUsage usage = usageExtractor != null ? usageExtractor(response) : AIServiceUsage.None;

            return new PasteAIProviderResult(response.Content, usage);
        }

        private Kernel CreateKernel(PasteAIRequest request)
        {
            var kernelBuilder = Kernel.CreateBuilder();

            switch (_providerType)
            {
                case "openai":
                    kernelBuilder.AddOpenAIChatCompletion(_config.Model, _config.ApiKey, serviceId: _config.Model);
                    break;

                case "azureopenai":
                    var deploymentName = string.IsNullOrWhiteSpace(_config.DeploymentName) ? _config.Model : _config.DeploymentName;
                    kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, _config.Endpoint, _config.ApiKey, serviceId: _config.Model);
                    break;

                default:
                    throw new NotSupportedException($"Provider '{_config.ProviderType}' is not supported by {nameof(SemanticKernelPasteProvider)}");
            }

            return kernelBuilder.Build();
        }

        private static string NormalizeProviderType(string providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
            {
                return "openai";
            }

            var normalized = providerType.Trim().ToLowerInvariant();

            return normalized switch
            {
                "azure" => "azureopenai",
                _ => normalized,
            };
        }
    }
}
