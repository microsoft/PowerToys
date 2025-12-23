// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;

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
            AIServiceType.AzureAIInference,
            AIServiceType.Ollama,
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
            var imageBytes = request.ImageBytes;

            if (string.IsNullOrWhiteSpace(prompt) || (string.IsNullOrWhiteSpace(inputText) && imageBytes is null))
            {
                throw new ArgumentException("Prompt and input content must be provided", nameof(request));
            }

<<<<<<< HEAD
=======
            var executionSettings = CreateExecutionSettings();
>>>>>>> main
            var kernel = CreateKernel();

            switch (_config.Usage)
            {
                case PasteAIUsage.TextToImage:
                    var imageDescription = string.IsNullOrWhiteSpace(prompt) ? inputText : $"{inputText}. {prompt}";
                    return await ProcessTextToImageAsync(kernel, imageDescription, cancellationToken);
                case PasteAIUsage.ChatCompletion:
                default:
                    var userMessageContent = $"""
                        User instructions:
                        {prompt}

                        Clipboard Content:
                        {inputText}

                        Output:
                        """;
                    return await ProcessChatCompletionAsync(kernel, request, userMessageContent, systemPrompt, cancellationToken);
            }
        }

        private async Task<string> ProcessTextToImageAsync(Kernel kernel, string userMessageContent, CancellationToken cancellationToken)
        {
#pragma warning disable SKEXP0001
            var imageService = kernel.GetRequiredService<ITextToImageService>();
            var settings = new OpenAITextToImageExecutionSettings
            {
                Size = (1024, 1024),
                ResponseFormat = "b64_json",
            };

            var generatedImages = await imageService.GetImageContentsAsync(new TextContent(userMessageContent), settings, cancellationToken: cancellationToken);

            if (generatedImages.Count == 0)
            {
                throw new InvalidOperationException("No image generated.");
            }

            var imageContent = generatedImages[0];

            if (imageContent.Data.HasValue)
            {
                var base64 = Convert.ToBase64String(imageContent.Data.Value.ToArray());
                return $"data:{imageContent.MimeType ?? "image/png"};base64,{base64}";
            }
            else if (imageContent.Uri != null)
            {
                using var client = new HttpClient();
                var imageBytes = await client.GetByteArrayAsync(imageContent.Uri, cancellationToken);
                var base64 = Convert.ToBase64String(imageBytes);
                return $"data:image/png;base64,{base64}";
            }
            else
            {
                throw new InvalidOperationException("Generated image contains no data.");
            }
#pragma warning restore SKEXP0001
        }

        private async Task<string> ProcessChatCompletionAsync(Kernel kernel, PasteAIRequest request, string userMessageContent, string systemPrompt, CancellationToken cancellationToken)
        {
            var executionSettings = CreateExecutionSettings();
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

            if (imageBytes != null)
            {
                var collection = new ChatMessageContentItemCollection();
                if (!string.IsNullOrWhiteSpace(inputText))
                {
                    collection.Add(new TextContent($"Clipboard Content:\n{inputText}"));
                }

                collection.Add(new ImageContent(imageBytes, request.ImageMimeType ?? "image/png"));
                collection.Add(new TextContent($"User instructions:\n{prompt}\n\nOutput:"));
                chatHistory.AddUserMessage(collection);
            }
            else
            {
                var userMessageContent = $"""
                    User instructions:
                    {prompt}

                    Clipboard Content:
                    {inputText}

                    Output:
                    """;
                chatHistory.AddUserMessage(userMessageContent);
            }

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
                    if (_config.Usage == PasteAIUsage.TextToImage)
                    {
#pragma warning disable SKEXP0010
                        kernelBuilder.AddOpenAITextToImage(apiKey, modelId: _config.Model);
#pragma warning restore SKEXP0010
                    }
                    else
                    {
                        kernelBuilder.AddOpenAIChatCompletion(_config.Model, apiKey, serviceId: _config.Model);
                    }

                    break;
                case AIServiceType.AzureOpenAI:
                    var deploymentName = string.IsNullOrWhiteSpace(_config.DeploymentName) ? _config.Model : _config.DeploymentName;
                    if (_config.Usage == PasteAIUsage.TextToImage)
                    {
#pragma warning disable SKEXP0010
                        kernelBuilder.AddAzureOpenAITextToImage(deploymentName, RequireEndpoint(endpoint, _serviceType), apiKey);
#pragma warning restore SKEXP0010
                    }
                    else
                    {
                        kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, RequireEndpoint(endpoint, _serviceType), apiKey, serviceId: _config.Model);
                    }

                    break;
                case AIServiceType.Mistral:
                    kernelBuilder.AddMistralChatCompletion(_config.Model, apiKey: apiKey);
                    break;
                case AIServiceType.Google:
                    kernelBuilder.AddGoogleAIGeminiChatCompletion(_config.Model, apiKey: apiKey);
                    break;
                case AIServiceType.AzureAIInference:
                    kernelBuilder.AddAzureAIInferenceChatCompletion(_config.Model, apiKey: apiKey, endpoint: new Uri(endpoint));
                    break;
                case AIServiceType.Ollama:
                    kernelBuilder.AddOllamaChatCompletion(_config.Model, endpoint: new Uri(endpoint));
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
