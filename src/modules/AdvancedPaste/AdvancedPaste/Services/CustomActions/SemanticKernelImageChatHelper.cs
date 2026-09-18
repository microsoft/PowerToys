// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.AI;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.MistralAI;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.CustomActions;

/// <summary>
/// Paste-local multimodal completion for clipboard images.
/// Shared <see cref="IAIChatProvider"/> is text-only; Advanced Paste keeps this SK path for images.
/// </summary>
internal static class SemanticKernelImageChatHelper
{
    public static bool SupportsImages(AIServiceType serviceType) =>
        serviceType is AIServiceType.OpenAI
            or AIServiceType.AzureOpenAI
            or AIServiceType.Mistral
            or AIServiceType.Google
            or AIServiceType.AzureAIInference
            or AIServiceType.Ollama
            or AIServiceType.Unknown;

    public static async Task<(string Content, AIServiceUsage Usage)> CompleteAsync(
        AIProviderConfig config,
        string systemPrompt,
        string prompt,
        string inputText,
        byte[] imageBytes,
        string imageMimeType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            throw new ArgumentException("System prompt must be provided", nameof(systemPrompt));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt must be provided", nameof(prompt));
        }

        var serviceType = config.ProviderType == AIServiceType.Unknown ? AIServiceType.OpenAI : config.ProviderType;
        if (!SupportsImages(serviceType))
        {
            throw new NotSupportedException($"Provider '{serviceType}' does not support image clipboard transforms.");
        }

        var kernel = CreateKernel(config, serviceType);
        var chatService = ResolveChatService(kernel, config.Model);
        var executionSettings = CreateExecutionSettings(serviceType);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        var collection = new ChatMessageContentItemCollection();
        if (!string.IsNullOrWhiteSpace(inputText))
        {
            collection.Add(new TextContent($"Clipboard Content:\n{inputText}"));
        }

        collection.Add(new ImageContent(imageBytes, imageMimeType ?? "image/png"));
        collection.Add(new TextContent($"User instructions:\n{prompt}\n\nOutput:"));
        chatHistory.AddUserMessage(collection);

        var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken);
        var usage = Helpers.AIServiceUsageHelper.GetOpenAIServiceUsage(response);
        return (response.Content ?? string.Empty, usage);
    }

    private static IChatCompletionService ResolveChatService(Kernel kernel, string modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            try
            {
                return kernel.GetRequiredService<IChatCompletionService>(modelId);
            }
            catch (Exception)
            {
                // Fall back when the connector did not register under the model id.
            }
        }

        return kernel.GetRequiredService<IChatCompletionService>();
    }

    private static Kernel CreateKernel(AIProviderConfig config, AIServiceType serviceType)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        var endpoint = string.IsNullOrWhiteSpace(config.Endpoint) ? null : config.Endpoint.Trim();
        var apiKey = config.ApiKey?.Trim() ?? string.Empty;
        var model = config.Model;

        if (RequiresApiKey(serviceType) && string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"API key is required for {serviceType} but was not provided.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"No model is configured for {serviceType}.");
        }

        switch (serviceType)
        {
            case AIServiceType.OpenAI:
                kernelBuilder.AddOpenAIChatCompletion(model, apiKey, serviceId: model);
                break;
            case AIServiceType.AzureOpenAI:
                var deploymentName = string.IsNullOrWhiteSpace(config.DeploymentName) ? model : config.DeploymentName;
                kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, RequireEndpoint(endpoint, serviceType), apiKey, serviceId: model);
                break;
            case AIServiceType.Mistral:
                kernelBuilder.AddMistralChatCompletion(model, apiKey: apiKey);
                break;
            case AIServiceType.Google:
                kernelBuilder.AddGoogleAIGeminiChatCompletion(model, apiKey: apiKey);
                break;
            case AIServiceType.AzureAIInference:
                kernelBuilder.AddAzureAIInferenceChatCompletion(model, apiKey: apiKey, endpoint: new Uri(RequireEndpoint(endpoint, serviceType)));
                break;
            case AIServiceType.Ollama:
                kernelBuilder.AddOllamaChatCompletion(model, endpoint: new Uri(RequireEndpoint(endpoint, serviceType)));
                break;
            default:
                throw new NotSupportedException($"Provider '{serviceType}' is not supported for image transforms.");
        }

        return kernelBuilder.Build();
    }

    // Exposed for unit tests guarding #49838 (ReasoningEffort must stay unset).
    internal static PromptExecutionSettings CreateExecutionSettings(AIServiceType serviceType)
    {
        return serviceType switch
        {
            AIServiceType.OpenAI or AIServiceType.AzureOpenAI => new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = null,
            },
            _ => new PromptExecutionSettings(),
        };
    }

    private static bool RequiresApiKey(AIServiceType serviceType) => serviceType != AIServiceType.Ollama;

    private static string RequireEndpoint(string endpoint, AIServiceType serviceType)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        throw new InvalidOperationException($"Endpoint is required for {serviceType} but was not provided.");
    }
}
