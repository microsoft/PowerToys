// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformService : ICustomActionTransformService
    {
        private const string DefaultSystemPrompt = """
            You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
            Do not output anything else besides the reformatted clipboard content.
            """;

        private readonly IPromptModerationService promptModerationService;
        private readonly IPasteAIProviderFactory providerFactory;
        private readonly IAICredentialsProvider credentialsProvider;
        private readonly IUserSettings userSettings;

        public CustomActionTransformService(IPromptModerationService promptModerationService, IPasteAIProviderFactory providerFactory, IAICredentialsProvider credentialsProvider, IUserSettings userSettings)
        {
            this.promptModerationService = promptModerationService;
            this.providerFactory = providerFactory;
            this.credentialsProvider = credentialsProvider;
            this.userSettings = userSettings;
        }

        public async Task<CustomActionTransformResult> TransformTextAsync(string prompt, string inputText, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var pasteConfig = userSettings?.PasteAIConfiguration;
            var providerConfig = BuildProviderConfig(pasteConfig);

            return await TransformAsync(prompt, inputText, providerConfig, cancellationToken, progress);
        }

        private async Task<CustomActionTransformResult> TransformAsync(string prompt, string inputText, PasteAIConfig providerConfig, CancellationToken cancellationToken, IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(providerConfig);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None);
            }

            if (string.IsNullOrWhiteSpace(inputText))
            {
                Logger.LogWarning("Clipboard has no usable text data");
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None);
            }

            var systemPrompt = providerConfig.SystemPrompt ?? DefaultSystemPrompt;

            var fullPrompt = (systemPrompt ?? string.Empty) + "\n\n" + (inputText ?? string.Empty);

            // await _promptModerationService.ValidateAsync(fullPrompt, cancellationToken);
            try
            {
                var provider = providerFactory.CreateProvider(providerConfig);

                var request = new PasteAIRequest
                {
                    Prompt = prompt,
                    InputText = inputText,
                    SystemPrompt = systemPrompt,
                };

                var providerContent = await provider.ProcessPasteAsync(
                    request,
                    cancellationToken,
                    progress);

                var usage = request.Usage;
                var content = providerContent ?? string.Empty;

                Logger.LogDebug($"{nameof(CustomActionTransformService)}.{nameof(TransformAsync)} complete; ModelName={providerConfig.Model ?? string.Empty}, PromptTokens={usage.PromptTokens}, CompletionTokens={usage.CompletionTokens}");

                return new CustomActionTransformResult(content, usage);
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

        private static AIServiceType NormalizeServiceType(AIServiceType serviceType)
        {
            return serviceType == AIServiceType.Unknown ? AIServiceType.OpenAI : serviceType;
        }

        private PasteAIConfig BuildProviderConfig(PasteAIConfiguration config)
        {
            config ??= new PasteAIConfiguration();
            var serviceType = NormalizeServiceType(config.ServiceTypeKind);
            var systemPrompt = string.IsNullOrWhiteSpace(config.SystemPrompt) ? DefaultSystemPrompt : config.SystemPrompt;
            var apiKey = AcquireApiKey(serviceType);
            var modelName = config.ModelName;

            var providerConfig = new PasteAIConfig
            {
                ProviderType = serviceType,
                ApiKey = apiKey,
                Model = modelName,
                Endpoint = config.EndpointUrl,
                DeploymentName = config.DeploymentName,
                ModelPath = config.ModelPath,
                SystemPrompt = systemPrompt,
            };

            return providerConfig;
        }

        private string AcquireApiKey(AIServiceType serviceType)
        {
            if (!RequiresApiKey(serviceType))
            {
                return string.Empty;
            }

            credentialsProvider.Refresh(AICredentialScope.PasteAI);
            return credentialsProvider.GetKey(AICredentialScope.PasteAI) ?? string.Empty;
        }

        private static bool RequiresApiKey(AIServiceType serviceType)
        {
            return serviceType is not (AIServiceType.Onnx or AIServiceType.FoundryLocal);
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

        private static string ResolveModelName(PasteAIConfiguration config, AIServiceType serviceType)
        {
            if (!string.IsNullOrWhiteSpace(config.ModelName))
            {
                return config.ModelName;
            }

            if (serviceType == AIServiceType.AzureOpenAI && !string.IsNullOrWhiteSpace(config.DeploymentName))
            {
                return config.DeploymentName;
            }

            return "gpt-3.5-turbo";
        }

        private static string ResolveModelIdentifier(PasteAIConfiguration config, AIServiceType serviceType, string resolvedModelName)
        {
            if (serviceType == AIServiceType.AzureOpenAI)
            {
                return string.IsNullOrWhiteSpace(config.DeploymentName) ? resolvedModelName : config.DeploymentName;
            }

            if (serviceType == AIServiceType.FoundryLocal && !string.IsNullOrWhiteSpace(resolvedModelName))
            {
                const string FoundryUrlPrefix = "fl://";
                return resolvedModelName.StartsWith(FoundryUrlPrefix, StringComparison.OrdinalIgnoreCase)
                    ? resolvedModelName
                    : $"{FoundryUrlPrefix}{resolvedModelName}";
            }

            return resolvedModelName;
        }

        private static Func<ChatMessageContent, AIServiceUsage> GetUsageExtractor(AIServiceType serviceType)
        {
            return serviceType switch
            {
                AIServiceType.OpenAI or AIServiceType.AzureOpenAI or AIServiceType.FoundryLocal => AIServiceUsageHelper.GetOpenAIServiceUsage,
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

        private static bool IsLocalProvider(AIServiceType serviceType)
        {
            return serviceType is AIServiceType.Onnx or AIServiceType.FoundryLocal;
        }
    }
}
