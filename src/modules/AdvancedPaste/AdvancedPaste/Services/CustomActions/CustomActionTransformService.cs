// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformService : ICustomActionTransformService
    {
        private const string DefaultSystemPrompt = """
            You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
            Do not output anything else besides the reformatted clipboard content.
            """;

        private readonly IPromptModerationService _promptModerationService;
        private readonly IPasteAIProviderFactory _providerFactory;
        private readonly IPasteAICredentialsProvider _aiCredentialsProvider;
        private readonly IUserSettings _userSettings;

        public CustomActionTransformService(IPromptModerationService promptModerationService, IPasteAIProviderFactory providerFactory, IPasteAICredentialsProvider aiCredentialsProvider, IUserSettings userSettings)
        {
            ArgumentNullException.ThrowIfNull(promptModerationService);
            ArgumentNullException.ThrowIfNull(providerFactory);
            ArgumentNullException.ThrowIfNull(aiCredentialsProvider);
            ArgumentNullException.ThrowIfNull(userSettings);

            _promptModerationService = promptModerationService;
            _providerFactory = providerFactory;
            _aiCredentialsProvider = aiCredentialsProvider;
            _userSettings = userSettings;
        }

        public async Task<Windows.ApplicationModel.DataTransfer.DataPackage> TransformTextToDataPackageAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var result = await TransformTextAsync(prompt, inputText, cancellationToken, progress);
            return AdvancedPaste.Helpers.DataPackageHelpers.CreateFromText(result?.Content ?? string.Empty);
        }

        public async Task<CustomActionTransformResult> TransformTextAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var context = CreateTransformContext(prompt, inputText);
            return await TransformAsync(context, cancellationToken, progress);
        }

        public async Task<CustomActionTransformResult> TransformAsync(CustomActionTransformContext context, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (string.IsNullOrWhiteSpace(context.Prompt))
            {
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None, new ChatHistory());
            }

            if (string.IsNullOrWhiteSpace(context.InputText))
            {
                Logger.LogWarning("Clipboard has no usable text data");
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None, new ChatHistory());
            }

            if (context.ProviderConfig is null)
            {
                throw new ArgumentException("Provider configuration must be supplied", nameof(context));
            }

            var chatHistory = new ChatHistory();
            var systemPrompt = context.SystemPrompt ?? context.ProviderConfig.SystemPrompt ?? DefaultSystemPrompt;
            chatHistory.AddSystemMessage(systemPrompt);

            var userMessage = $"""
                User instructions:
                {context.Prompt}

                Clipboard Content:
                {context.InputText}

                Output:
                """;
            chatHistory.AddUserMessage(userMessage);

            var fullPrompt = GetFullPrompt(chatHistory);

            // await _promptModerationService.ValidateAsync(fullPrompt, cancellationToken);
            try
            {
                var provider = _providerFactory.CreateProvider(context.ProviderConfig);

                var providerResult = await provider.ProcessPasteAsync(
                    new PasteAIRequest
                    {
                        ChatHistory = chatHistory,
                        ExecutionSettings = context.ExecutionSettings,
                        ModelId = context.ModelId,
                        UsageExtractor = context.UsageExtractor,
                    },
                    cancellationToken,
                    progress);

                var usage = providerResult?.Usage ?? AIServiceUsage.None;
                var content = providerResult?.Content ?? string.Empty;

                var modelName = context.ModelId;
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = context.ProviderConfig?.Model;
                }

                Logger.LogDebug($"{nameof(CustomActionTransformService)}.{nameof(TransformAsync)} complete; ModelName={modelName ?? string.Empty}, PromptTokens={usage.PromptTokens}, CompletionTokens={usage.CompletionTokens}");

                return new CustomActionTransformResult(content, usage, chatHistory);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{nameof(CustomActionTransformService)}.{nameof(TransformAsync)} failed", ex);

                if (ex is PasteActionException or OperationCanceledException)
                {
                    throw;
                }

                throw new PasteActionException(ErrorHelpers.TranslateErrorText(-1), ex);
            }
        }

        private static string GetFullPrompt(ChatHistory chatHistory)
        {
            if (chatHistory.Count == 0)
            {
                throw new ArgumentException("Chat history must not be empty", nameof(chatHistory));
            }

            int numSystemMessages = chatHistory.Count - 1;
            var systemMessages = chatHistory.Take(numSystemMessages);
            var userPromptMessage = chatHistory.Last();

            if (systemMessages.Any(message => message.Role != AuthorRole.System))
            {
                throw new ArgumentException("Chat history must start with system messages", nameof(chatHistory));
            }

            if (userPromptMessage.Role != AuthorRole.User)
            {
                throw new ArgumentException("Chat history must end with a user message", nameof(chatHistory));
            }

            var newLine = Environment.NewLine;

            var combinedSystemMessage = string.Join(newLine, systemMessages.Select(message => message.Content));
            return $"{combinedSystemMessage}{newLine}{newLine}User instructions:{newLine}{userPromptMessage.Content}";
        }

        private CustomActionTransformContext CreateTransformContext(string prompt, string inputText)
        {
            var pasteConfig = _userSettings?.PasteAIConfiguration ?? new PasteAIConfiguration();
            var providerType = NormalizeProviderType(pasteConfig.ServiceType);
            var executionSettings = CreateExecutionSettings(pasteConfig);
            var usageExtractor = GetUsageExtractor(providerType);
            var systemPrompt = ExtractSystemPrompt(pasteConfig);
            var requiresApiKey = RequiresApiKey(providerType);

            if (requiresApiKey)
            {
                _aiCredentialsProvider.Refresh();
            }

            var modelName = ResolveModelName(pasteConfig, providerType);
            var modelId = ResolveModelIdentifier(pasteConfig, providerType, modelName);
            var isLocal = IsLocalProvider(providerType);
            var providerConfig = new PasteAIConfig
            {
                ProviderType = providerType,
                ApiKey = requiresApiKey ? _aiCredentialsProvider.Key : string.Empty,
                Model = modelName,
                Endpoint = pasteConfig.EndpointUrl,
                DeploymentName = pasteConfig.DeploymentName,
                LocalModelPath = isLocal ? pasteConfig.ModelPath : null,
                ModelPath = isLocal ? null : pasteConfig.ModelPath,
                IsLocal = isLocal,
                ExecutionSettings = executionSettings,
                UsageExtractor = usageExtractor,
                SystemPrompt = systemPrompt,
            };

            return new CustomActionTransformContext
            {
                Prompt = prompt,
                InputText = inputText,
                ProviderConfig = providerConfig,
                ExecutionSettings = executionSettings,
                ModelId = modelId,
                UsageExtractor = usageExtractor,
                SystemPrompt = systemPrompt,
            };
        }

        private static string NormalizeProviderType(string serviceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                return "openai";
            }

            var normalized = serviceType.Trim().ToLowerInvariant();

            return normalized switch
            {
                "azure" => "azureopenai",
                _ => normalized,
            };
        }

        private static bool RequiresApiKey(string providerType)
        {
            return providerType is not ("local" or "onnx");
        }

        private static bool IsLocalProvider(string providerType)
        {
            return providerType is "local" or "onnx";
        }

        private static OpenAIPromptExecutionSettings CreateExecutionSettings(PasteAIConfiguration config)
        {
            return new OpenAIPromptExecutionSettings
            {
                Temperature = 0.01,
                MaxTokens = 2000,
                FunctionChoiceBehavior = null,
            };
        }

        private static string ResolveModelName(PasteAIConfiguration config, string providerType)
        {
            if (!string.IsNullOrWhiteSpace(config.ModelName))
            {
                return config.ModelName;
            }

            if (providerType == "azureopenai" && !string.IsNullOrWhiteSpace(config.DeploymentName))
            {
                return config.DeploymentName;
            }

            return "gpt-3.5-turbo";
        }

        private static string ResolveModelIdentifier(PasteAIConfiguration config, string providerType, string resolvedModelName)
        {
            if (providerType == "azureopenai")
            {
                return string.IsNullOrWhiteSpace(config.DeploymentName) ? resolvedModelName : config.DeploymentName;
            }

            return resolvedModelName;
        }

        private static Func<ChatMessageContent, AIServiceUsage> GetUsageExtractor(string providerType)
        {
            return providerType switch
            {
                "openai" or "azureopenai" => AIServiceUsageHelper.GetOpenAIServiceUsage,
                _ => null,
            };
        }

        private static string ExtractSystemPrompt(PasteAIConfiguration config)
        {
            if (config is null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(config.SystemPrompt) ? null : config.SystemPrompt;
        }
    }
}
