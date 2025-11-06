// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Services.CustomActions;
using AdvancedPaste.Settings;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public abstract class KernelServiceBase(
    IKernelQueryCacheService queryCacheService,
    IPromptModerationService promptModerationService,
    IUserSettings userSettings,
    ICustomActionTransformService customActionTransformService) : IKernelService
{
    private const string PromptParameterName = "prompt";

    private readonly IKernelQueryCacheService _queryCacheService = queryCacheService;
    private readonly IPromptModerationService _promptModerationService = promptModerationService;
    private readonly IUserSettings _userSettings = userSettings;
    private readonly ICustomActionTransformService _customActionTransformService = customActionTransformService;

    protected abstract string AdvancedAIModelName { get; }

    protected abstract PromptExecutionSettings PromptExecutionSettings { get; }

    protected abstract void AddChatCompletionService(IKernelBuilder kernelBuilder);

    protected abstract AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage);

    protected abstract IKernelRuntimeConfiguration GetRuntimeConfiguration();

    public async Task<DataPackage> TransformClipboardAsync(string prompt, DataPackageView clipboardData, bool isSavedQuery, CancellationToken cancellationToken, IProgress<double> progress)
    {
        Logger.LogTrace();

        var kernel = CreateKernel();
        kernel.SetDataPackageView(clipboardData);
        kernel.SetCancellationToken(cancellationToken);
        kernel.SetProgress(progress);

        CacheKey cacheKey = new() { Prompt = prompt, AvailableFormats = await clipboardData.GetAvailableFormatsAsync() };
        var maybeCacheValue = _queryCacheService.ReadOrNull(cacheKey);
        bool cacheUsed = maybeCacheValue != null;

        ChatHistory chatHistory = [];

        try
        {
            (chatHistory, var usage) = cacheUsed ? await ExecuteCachedActionChain(kernel, maybeCacheValue.ActionChain) : await ExecuteAICompletion(kernel, prompt, cancellationToken);

            LogResult(cacheUsed, isSavedQuery, kernel.GetOrAddActionChain(), usage);

            if (kernel.GetLastError() is Exception ex)
            {
                throw ex;
            }

            var outputPackage = kernel.GetDataPackage();

            if (!(await outputPackage.GetView().HasUsableDataAsync()))
            {
                throw new InvalidOperationException("No data was returned from the kernel operation");
            }

            if (!cacheUsed)
            {
                await _queryCacheService.WriteAsync(cacheKey, new CacheValue(kernel.GetOrAddActionChain()));
            }

            Logger.LogDebug($"Kernel operation done: \n{FormatChatHistory(chatHistory)}");

            return outputPackage;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error executing kernel operation", ex);
            Logger.LogError($"Kernel operation Error: \n{FormatChatHistory(chatHistory)}");

            AdvancedPasteSemanticKernelErrorEvent errorEvent = new(ex is PasteActionModeratedException ? PasteActionModeratedException.ErrorDescription : ex.Message);
            PowerToysTelemetry.Log.WriteEvent(errorEvent);

            if (ex is PasteActionException or OperationCanceledException)
            {
                throw;
            }
            else
            {
                var message = ex is HttpOperationException httpOperationEx
                    ? ErrorHelpers.TranslateErrorText((int?)httpOperationEx.StatusCode ?? -1)
                    : ResourceLoaderInstance.ResourceLoader.GetString("PasteError");

                var lastAssistantMessage = chatHistory.LastOrDefault(chatMessage => chatMessage.Role == AuthorRole.Assistant)?.ToString();
                throw new PasteActionException(message, innerException: ex, aiServiceMessage: lastAssistantMessage);
            }
        }
    }

    private static string GetFullPrompt(ChatHistory initialHistory)
    {
        if (initialHistory.Count == 0)
        {
            throw new ArgumentException("Chat history must not be empty", nameof(initialHistory));
        }

        int numSystemMessages = initialHistory.Count - 1;
        var systemMessages = initialHistory.Take(numSystemMessages);
        var userPromptMessage = initialHistory.Last();

        if (systemMessages.Any(message => message.Role != AuthorRole.System))
        {
            throw new ArgumentException("Chat history must start with system messages", nameof(initialHistory));
        }

        if (userPromptMessage.Role != AuthorRole.User)
        {
            throw new ArgumentException("Chat history must end with a user message", nameof(initialHistory));
        }

        var newLine = Environment.NewLine;

        var combinedSystemMessage = string.Join(newLine, systemMessages.Select(message => message.Content));
        return $"{combinedSystemMessage}{newLine}{newLine}User instructions:{newLine}{userPromptMessage.Content}";
    }

    private async Task<(ChatHistory ChatHistory, AIServiceUsage Usage)> ExecuteAICompletion(Kernel kernel, string prompt, CancellationToken cancellationToken)
    {
        var runtimeConfig = GetRuntimeConfiguration();

        ChatHistory chatHistory = [];

        chatHistory.AddSystemMessage(runtimeConfig.SystemPrompt);
        chatHistory.AddSystemMessage($"Available clipboard formats: {await kernel.GetDataFormatsAsync()}");
        chatHistory.AddUserMessage(prompt);

        if (ShouldModerateAdvancedAI())
        {
            await _promptModerationService.ValidateAsync(GetFullPrompt(chatHistory), cancellationToken);
        }

        var chatResult = await kernel.GetRequiredService<IChatCompletionService>(AdvancedAIModelName)
                                     .GetChatMessageContentAsync(chatHistory, PromptExecutionSettings, kernel, cancellationToken);
        chatHistory.Add(chatResult);

        var totalUsage = chatHistory.Select(GetAIServiceUsage)
                                    .Aggregate(AIServiceUsage.Add);

        return (chatHistory, totalUsage);
    }

    private async Task<(ChatHistory ChatHistory, AIServiceUsage Usage)> ExecuteCachedActionChain(Kernel kernel, List<ActionChainItem> actionChain)
    {
        foreach (var item in actionChain)
        {
            kernel.GetCancellationToken().ThrowIfCancellationRequested();

            if (item.Arguments.Count > 0)
            {
                await ExecutePromptTransformAsync(kernel, item.Format, item.Arguments[PromptParameterName]);
            }
            else
            {
                await ExecuteStandardTransformAsync(kernel, item.Format);
            }
        }

        return ([], AIServiceUsage.None);
    }

    protected IUserSettings UserSettings => _userSettings;

    private void LogResult(bool cacheUsed, bool isSavedQuery, IEnumerable<ActionChainItem> actionChain, AIServiceUsage usage)
    {
        AdvancedPasteSemanticKernelFormatEvent telemetryEvent = new(cacheUsed, isSavedQuery, usage.PromptTokens, usage.CompletionTokens, AdvancedAIModelName, AdvancedPasteSemanticKernelFormatEvent.FormatActionChain(actionChain));
        PowerToysTelemetry.Log.WriteEvent(telemetryEvent);

        // Log endpoint usage
        var runtimeConfig = GetRuntimeConfiguration();
        var endpointEvent = new AdvancedPasteEndpointUsageEvent(runtimeConfig.ServiceType);
        PowerToysTelemetry.Log.WriteEvent(endpointEvent);

        var logEvent = new AIServiceFormatEvent(telemetryEvent);
        Logger.LogDebug($"{nameof(TransformClipboardAsync)} complete; {logEvent.ToJsonString()}");
    }

    private Kernel CreateKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        AddChatCompletionService(kernelBuilder);
        kernelBuilder.Plugins.AddFromFunctions("Actions", GetKernelFunctions());
        return kernelBuilder.Build();
    }

    private IEnumerable<KernelFunction> GetKernelFunctions()
    {
        // Get standard format functions
        var standardFunctions =
            from format in Enum.GetValues<PasteFormats>()
            let metadata = PasteFormat.MetadataDict[format]
            let coreDescription = metadata.KernelFunctionDescription
            where !string.IsNullOrEmpty(coreDescription)
            let requiresPrompt = metadata.RequiresPrompt
            orderby requiresPrompt descending
            select KernelFunctionFactory.CreateFromMethod(
                method: requiresPrompt ? async (Kernel kernel, string prompt) => await ExecutePromptTransformAsync(kernel, format, prompt)
                                       : async (Kernel kernel) => await ExecuteStandardTransformAsync(kernel, format),
                functionName: format.ToString(),
                description: requiresPrompt ? coreDescription : $"{coreDescription} Puts the result back on the clipboard.",
                parameters: requiresPrompt ? [new(PromptParameterName) { Description = "Input instructions to AI", ParameterType = typeof(string) }] : null,
                returnParameter: new() { Description = "Array of available clipboard formats after operation" });

        HashSet<string> usedFunctionNames = new(Enum.GetNames<PasteFormats>(), StringComparer.OrdinalIgnoreCase);

        // Get custom action functions
        var customActionFunctions = _userSettings.CustomActions
            .Where(customAction => !string.IsNullOrWhiteSpace(customAction.Name) && !string.IsNullOrWhiteSpace(customAction.Prompt))
            .Select(customAction =>
            {
                var sanitizedBaseName = SanitizeFunctionName(customAction.Name);
                var functionName = GetUniqueFunctionName(sanitizedBaseName, usedFunctionNames, customAction.Id);
                var description = string.IsNullOrWhiteSpace(customAction.Description)
                    ? $"Runs the \"{customAction.Name}\" custom action."
                    : customAction.Description;
                return KernelFunctionFactory.CreateFromMethod(
                    method: async (Kernel kernel) => await ExecuteCustomActionAsync(kernel, customAction.Prompt),
                    functionName: functionName,
                    description: description,
                    parameters: null,
                    returnParameter: new() { Description = "Array of available clipboard formats after operation" });
            });

        return standardFunctions.Concat(customActionFunctions);
    }

    private static string GetUniqueFunctionName(string baseName, HashSet<string> usedFunctionNames, int customActionId)
    {
        ArgumentNullException.ThrowIfNull(usedFunctionNames);

        var candidate = string.IsNullOrEmpty(baseName) ? "_CustomAction" : baseName;

        if (usedFunctionNames.Add(candidate))
        {
            return candidate;
        }

        int suffix = 1;
        while (true)
        {
            var nextCandidate = $"{candidate}_{customActionId}_{suffix}";
            if (usedFunctionNames.Add(nextCandidate))
            {
                return nextCandidate;
            }

            suffix++;
        }
    }

    private static string SanitizeFunctionName(string name)
    {
        // Remove invalid characters and ensure the function name is valid for kernel
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        // Ensure it starts with a letter or underscore
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "_" + sanitized;
        }

        // Ensure it's not empty
        return string.IsNullOrEmpty(sanitized) ? "_CustomAction" : sanitized;
    }

    private Task<string> ExecuteCustomActionAsync(Kernel kernel, string fixedPrompt) =>
        ExecuteTransformAsync(
            kernel,
            new ActionChainItem(PasteFormats.CustomTextTransformation, Arguments: new() { { PromptParameterName, fixedPrompt } }),
            async dataPackageView =>
            {
                var input = await dataPackageView.GetClipboardTextOrThrowAsync(kernel.GetCancellationToken());
                var result = await _customActionTransformService.TransformTextAsync(fixedPrompt, input, kernel.GetCancellationToken(), kernel.GetProgress());
                return DataPackageHelpers.CreateFromText(result?.Content ?? string.Empty);
            });

    private Task<string> ExecutePromptTransformAsync(Kernel kernel, PasteFormats format, string prompt) =>
        ExecuteTransformAsync(
            kernel,
            new ActionChainItem(format, Arguments: new() { { PromptParameterName, prompt } }),
            async dataPackageView =>
            {
                var input = await dataPackageView.GetClipboardTextOrThrowAsync(kernel.GetCancellationToken());
                string output = await GetPromptBasedOutput(format, prompt, input, kernel.GetCancellationToken(), kernel.GetProgress());
                return DataPackageHelpers.CreateFromText(output);
            });

    private async Task<string> GetPromptBasedOutput(PasteFormats format, string prompt, string input, CancellationToken cancellationToken, IProgress<double> progress) =>
        format switch
        {
            PasteFormats.CustomTextTransformation => (await _customActionTransformService.TransformTextAsync(prompt, input, cancellationToken, progress))?.Content ?? string.Empty,
            _ => throw new ArgumentException($"Unsupported format {format} for prompt transform", nameof(format)),
        };

    private Task<string> ExecuteStandardTransformAsync(Kernel kernel, PasteFormats format) =>
        ExecuteTransformAsync(
           kernel,
           new ActionChainItem(format, Arguments: []),
           async dataPackageView => await TransformHelpers.TransformAsync(format, dataPackageView, kernel.GetCancellationToken(), kernel.GetProgress()));

    private static async Task<string> ExecuteTransformAsync(Kernel kernel, ActionChainItem actionChainItem, Func<DataPackageView, Task<DataPackage>> transformFunc)
    {
        kernel.GetOrAddActionChain().Add(actionChainItem);
        kernel.SetLastError(null);

        try
        {
            var input = kernel.GetDataPackageView();
            var output = await transformFunc(input);
            kernel.SetDataPackage(output);
            return await kernel.GetDataFormatsAsync();
        }
        catch (Exception ex)
        {
            kernel.SetLastError(ex);
            throw;
        }
    }

    private string FormatChatHistory(ChatHistory chatHistory) =>
        chatHistory.Count == 0 ? "[No chat history]" : string.Join(Environment.NewLine, chatHistory.Select(FormatChatMessage));

    private string FormatChatMessage(ChatMessageContent chatMessage)
    {
        static string Redact(object data) =>
#if DEBUG
            data?.ToString();
#else
            "[Redacted]";
#endif

        static string FormatKernelArguments(KernelArguments kernelArguments) =>
            string.Join(", ", kernelArguments?.Select(argument => $"{argument.Key}: {Redact(argument.Value)}") ?? []);

        static string FormatKernelContent(KernelContent kernelContent) =>
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            kernelContent switch
            {
                FunctionCallContent functionCallContent => $"{functionCallContent.FunctionName}({FormatKernelArguments(functionCallContent.Arguments)})",
                FunctionResultContent functionResultContent => functionResultContent.FunctionName,
                _ => kernelContent.ToString(),
            };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var role = chatMessage.Role;
        var content = string.Join(" / ", chatMessage.Items.Select(FormatKernelContent));
        var redactedContent = role == AuthorRole.System || role == AuthorRole.Tool ? content : Redact(content);
        var usage = GetAIServiceUsage(chatMessage);
        var usageString = usage.HasUsage ? $" [{usage}]" : string.Empty;
        return $"-> {role}: {redactedContent}{usageString}";
    }

    protected virtual bool ShouldModerateAdvancedAI()
    {
        return false;
    }
}
