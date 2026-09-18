// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.AI;
using Microsoft.PowerToys.AI.Configuration;
using Microsoft.PowerToys.AI.Credentials;
using Microsoft.PowerToys.AI.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using AIServiceUsage = Microsoft.PowerToys.AI.AIServiceUsage;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformService : ICustomActionTransformService
    {
        private const string ModuleName = "AdvancedPaste";

        private const string DefaultSystemPrompt = """
            You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
            Do not output anything else besides the reformatted clipboard content.
            """;

        private readonly IPromptModerationService promptModerationService;
        private readonly IAIChatProviderFactory chatProviderFactory;
        private readonly IAICredentialsProvider credentialsProvider;
        private readonly IUserSettings userSettings;

        public CustomActionTransformService(
            IPromptModerationService promptModerationService,
            IAIChatProviderFactory chatProviderFactory,
            IAICredentialsProvider credentialsProvider,
            IUserSettings userSettings)
        {
            this.promptModerationService = promptModerationService;
            this.chatProviderFactory = chatProviderFactory;
            this.credentialsProvider = credentialsProvider;
            this.userSettings = userSettings;
        }

        public async Task<CustomActionTransformResult> TransformAsync(string prompt, string inputText, byte[] imageBytes, CancellationToken cancellationToken, IProgress<double> progress, string systemPromptOverride = null, string providerIdOverride = null)
        {
            var aiConfiguration = userSettings?.PasteAIConfiguration;
            var (providerConfig, providerId) = BuildProviderConfig(aiConfiguration, providerIdOverride);

            if (systemPromptOverride != null)
            {
                providerConfig.SystemPrompt = systemPromptOverride;
            }

            return await TransformAsync(prompt, inputText, imageBytes, providerConfig, providerId, cancellationToken, progress);
        }

        private async Task<CustomActionTransformResult> TransformAsync(
            string prompt,
            string inputText,
            byte[] imageBytes,
            AIProviderConfig providerConfig,
            string providerId,
            CancellationToken cancellationToken,
            IProgress<double> progress)
        {
            ArgumentNullException.ThrowIfNull(providerConfig);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None);
            }

            if (string.IsNullOrWhiteSpace(inputText) && imageBytes is null)
            {
                Logger.LogWarning("Clipboard has no usable data");
                return new CustomActionTransformResult(string.Empty, AIServiceUsage.None);
            }

            var systemPrompt = string.IsNullOrWhiteSpace(providerConfig.SystemPrompt) ? DefaultSystemPrompt : providerConfig.SystemPrompt;
            providerConfig.SystemPrompt = systemPrompt;

            var fullPrompt = systemPrompt + "\n\n" + (inputText ?? string.Empty);

            if (ShouldModerate(providerConfig))
            {
                await promptModerationService.ValidateAsync(fullPrompt, cancellationToken);
            }

            try
            {
                var operationStart = DateTime.UtcNow;
                string content;
                AIServiceUsage usage;

                if (imageBytes != null)
                {
                    (content, usage) = await SemanticKernelImageChatHelper.CompleteAsync(
                        providerConfig,
                        systemPrompt,
                        prompt,
                        inputText,
                        imageBytes,
                        "image/png",
                        cancellationToken);
                }
                else
                {
                    var chatProvider = chatProviderFactory.CreateProvider(providerConfig);
                    var userTurn = AIChatTurn.User(BuildUserPrompt(prompt, inputText));
                    content = await chatProvider.CompleteAsync(systemPrompt, [userTurn], cancellationToken);
                    usage = AIServiceUsage.None;
                }

                progress?.Report(1.0);

                var durationMs = (int)Math.Round((DateTime.UtcNow - operationStart).TotalMilliseconds);
                content ??= string.Empty;

                var isLocal = AIProviderConfigMapper.IsLocalProvider(providerConfig.ProviderType);
                PowerToysTelemetry.Log.WriteEvent(new AIEndpointUsageEvent(
                    ModuleName,
                    providerConfig.ProviderType,
                    providerConfig.Model ?? string.Empty,
                    providerId,
                    isLocal,
                    isAdvanced: false,
                    durationMs,
                    usage.PromptTokens,
                    usage.CompletionTokens));

                // Keep paste-local event for existing dashboards during transition.
                PowerToysTelemetry.Log.WriteEvent(new AdvancedPasteEndpointUsageEvent(
                    providerConfig.ProviderType,
                    providerConfig.Model ?? string.Empty,
                    isAdvanced: false,
                    durationMs: durationMs));

                Logger.LogDebug($"{nameof(CustomActionTransformService)}.{nameof(TransformAsync)} complete; ModelName={providerConfig.Model ?? string.Empty}, PromptTokens={usage.PromptTokens}, CompletionTokens={usage.CompletionTokens}, DurationMs={durationMs}");

                return new CustomActionTransformResult(content, usage);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{nameof(CustomActionTransformService)}.{nameof(TransformAsync)} failed", ex);
                var statusCode = ExtractStatusCode(ex);
                var modelName = providerConfig.Model ?? string.Empty;
                var errorDescription = ex is PasteActionModeratedException
                    ? PasteActionModeratedException.ErrorDescription
                    : ex.Message;

                PowerToysTelemetry.Log.WriteEvent(new AICompletionErrorEvent(
                    ModuleName,
                    providerConfig.ProviderType,
                    modelName,
                    statusCode,
                    errorDescription,
                    providerId));

                AdvancedPasteCustomActionErrorEvent errorEvent = new(providerConfig.ProviderType, modelName, statusCode, errorDescription);
                PowerToysTelemetry.Log.WriteEvent(errorEvent);

                if (ex is PasteActionException or OperationCanceledException)
                {
                    throw;
                }

                var failureMessage = providerConfig.ProviderType switch
                {
                    AIServiceType.OpenAI or AIServiceType.AzureOpenAI => ErrorHelpers.TranslateErrorText(statusCode),
                    _ => ResourceLoaderInstance.ResourceLoader.GetString("PasteError"),
                };

                throw new PasteActionException(failureMessage, ex);
            }
        }

        private static string BuildUserPrompt(string prompt, string inputText) =>
            $"""
            User instructions:
            {prompt}

            Clipboard Content:
            {inputText}

            Output:
            """;

        private static int ExtractStatusCode(Exception exception)
        {
            if (exception is HttpOperationException httpOperationException)
            {
                return (int?)httpOperationException.StatusCode ?? -1;
            }

            if (exception is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode)
            {
                return (int)statusCode;
            }

            return -1;
        }

        private (AIProviderConfig Config, string ProviderId) BuildProviderConfig(AIConfiguration config, string providerIdOverride = null)
        {
            config ??= new AIConfiguration();
            var provider = AIProviderConfigMapper.ResolveProvider(config, providerIdOverride)
                           ?? config.Providers?.FirstOrDefault()
                           ?? new AIProviderDefinition();

            var providerConfig = AIProviderConfigMapper.FromDefinition(provider, credentialsProvider);
            if (string.IsNullOrWhiteSpace(providerConfig.SystemPrompt))
            {
                providerConfig.SystemPrompt = DefaultSystemPrompt;
            }

            return (providerConfig, provider.Id ?? string.Empty);
        }

        private static bool ShouldModerate(AIProviderConfig providerConfig)
        {
            if (providerConfig is null || !providerConfig.ModerationEnabled)
            {
                return false;
            }

            return providerConfig.ProviderType == AIServiceType.OpenAI || providerConfig.ProviderType == AIServiceType.AzureOpenAI;
        }
    }
}
