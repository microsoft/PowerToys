// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services;

public abstract class KernelServiceBase(IKernelQueryCacheService queryCacheService, IPromptModerationService promptModerationService, ICustomTextTransformService customTextTransformService) : IKernelService
{
    private const string PromptParameterName = "prompt";

    private readonly IKernelQueryCacheService _queryCacheService = queryCacheService;
    private readonly IPromptModerationService _promptModerationService = promptModerationService;
    private readonly ICustomTextTransformService _customTextTransformService = customTextTransformService;

    protected abstract string ModelName { get; }

    protected abstract PromptExecutionSettings PromptExecutionSettings { get; }

    protected abstract void AddChatCompletionService(IKernelBuilder kernelBuilder);

    protected abstract AIServiceUsage GetAIServiceUsage(ChatMessageContent chatMessage);

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
        ChatHistory chatHistory = [];

        chatHistory.AddSystemMessage("""
                You are an agent who is tasked with helping users paste their clipboard data. You have functions available to help you with this task.
                You never need to ask permission, always try to do as the user asks. The user will only input one message and will not be available for further questions, so try your best.
                The user will put in a request to format their clipboard data and you will fulfill it.
                You will not directly see the output clipboard content, and do not need to provide it in the chat. You just need to do the transform operations as needed.
                If you are unable to fulfill the request, end with an error message in the language of the user's request.
                """);
        chatHistory.AddSystemMessage($"Available clipboard formats: {await kernel.GetDataFormatsAsync()}");
        chatHistory.AddUserMessage(prompt);

        await _promptModerationService.ValidateAsync(GetFullPrompt(chatHistory), cancellationToken);

        var chatResult = await kernel.GetRequiredService<IChatCompletionService>()
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

    private void LogResult(bool cacheUsed, bool isSavedQuery, IEnumerable<ActionChainItem> actionChain, AIServiceUsage usage)
    {
        AdvancedPasteSemanticKernelFormatEvent telemetryEvent = new(cacheUsed, isSavedQuery, usage.PromptTokens, usage.CompletionTokens, ModelName, AdvancedPasteSemanticKernelFormatEvent.FormatActionChain(actionChain));
        PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
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

    private IEnumerable<KernelFunction> GetKernelFunctions() =>
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

    private Task<string> ExecutePromptTransformAsync(Kernel kernel, PasteFormats format, string prompt) =>
        ExecuteTransformAsync(
            kernel,
            new ActionChainItem(format, Arguments: new() { { PromptParameterName, prompt } }),
            async dataPackageView =>
            {
                var input = await dataPackageView.GetTextAsync();
                string output = await GetPromptBasedOutput(format, prompt, input, kernel.GetCancellationToken(), kernel.GetProgress());
                return DataPackageHelpers.CreateFromText(output);
            });

    private async Task<string> GetPromptBasedOutput(PasteFormats format, string prompt, string input, CancellationToken cancellationToken, IProgress<double> progress) =>
        format switch
        {
            PasteFormats.CustomTextTransformation => await _customTextTransformService.TransformTextAsync(prompt, input, cancellationToken, progress),
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
}
