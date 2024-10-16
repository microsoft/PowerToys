// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Telemetry;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Services.OpenAI;

public sealed class KernelService(IAICredentialsProvider aiCredentialsProvider, ICustomTextTransformService customTextTransformService) : IKernelService
{
    private const string ModelName = "gpt-4o";
    private readonly IAICredentialsProvider _aiCredentialsProvider = aiCredentialsProvider;
    private readonly ICustomTextTransformService _customTextTransformService = customTextTransformService;

    public async Task<DataPackage> TransformClipboardAsync(string inputInstructions, DataPackageView clipboardData)
    {
        Logger.LogTrace();

        var kernel = CreateKernel();
        kernel.SetDataPackageView(clipboardData);

        OpenAIPromptExecutionSettings executionSettings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.01,
        };

        ChatHistory chatHistory = [];

        chatHistory.AddSystemMessage("""
                You are an agent who is tasked with helping users paste their clipboard data. You have functions available to help you with this task.
                You never need to ask permission, always try to do as the user asks. The user will only input one message and will not be available for further questions, so try your best.
                The user will put in a request to format their clipboard data and you will fulfill it.
                You will not directly see the output clipboard content, and do not need to provide it in the chat. You just need to do the transform operations as needed.
                """);
        chatHistory.AddSystemMessage($"Available clipboard formats: {await kernel.GetDataFormatsAsync()}");
        chatHistory.AddUserMessage(inputInstructions);

        try
        {
            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
            chatHistory.AddAssistantMessage(result.Content);

            var usage = result.Metadata.GetValueOrDefault("Usage") as CompletionsUsage;

            AdvancedPasteSemanticKernelFormatEvent telemetryEvent = new(usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0, ModelName, AdvancedPasteSemanticKernelFormatEvent.FormatActionChain(kernel.GetActionChain()));
            PowerToysTelemetry.Log.WriteEvent(telemetryEvent);

            var logEvent = new { telemetryEvent.PromptTokens, telemetryEvent.CompletionTokens, telemetryEvent.ModelName, telemetryEvent.UsedActionChain };
            Logger.LogDebug($"{nameof(TransformClipboardAsync)} complete; {JsonSerializer.Serialize(logEvent)}");

            if (kernel.GetLastError() is Exception ex)
            {
                throw ex;
            }

            var outputPackage = kernel.GetDataPackage();

            if (result == null || !(await outputPackage.GetView().HasUsableDataAsync()))
            {
                throw new InvalidOperationException("No data was returned from the completion operation");
            }

            Logger.LogDebug($"Completion done: \n{FormatChatHistory(chatHistory)}");
            return outputPackage;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error executing completion", ex);
            Logger.LogError($"Completion Error: \n{FormatChatHistory(chatHistory)}");

            if (ex is HttpOperationException error)
            {
                throw new PasteActionException(ErrorHelpers.TranslateErrorText((int?)error.StatusCode ?? -1), error);
            }
            else
            {
                throw;
            }
        }
    }

    private Kernel CreateKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder().AddOpenAIChatCompletion(ModelName, _aiCredentialsProvider.Key);
        kernelBuilder.Plugins.AddFromFunctions("Actions", CreateKernelFunctions());
        return kernelBuilder.Build();
    }

    private IEnumerable<KernelFunction> CreateKernelFunctions()
    {
        KernelReturnParameterMetadata returnParameter = new() { Description = "Array of available clipboard formats after operation" };

        var customTextTransformFunction = KernelFunctionFactory.CreateFromMethod(
            method: ExecuteCustomTextTransformAsync,
            functionName: nameof(PasteFormats.CustomTextTransformation),
            description: PasteFormat.MetadataDict[PasteFormats.CustomTextTransformation].KernelFunctionDescription,
            parameters: [new("inputInstructions") { Description = "Input instructions to AI", ParameterType = typeof(string) }],
            returnParameter);

        var standardTransformFunctions = from format in Enum.GetValues<PasteFormats>()
                                         let metadata = PasteFormat.MetadataDict[format]
                                         let coreDescription = metadata.KernelFunctionDescription
                                         where !string.IsNullOrEmpty(coreDescription) && !metadata.IsInternalAction
                                         select KernelFunctionFactory.CreateFromMethod(
                                             method: async (Kernel kernel) => await ExecuteStandardTransformAsync(kernel, format),
                                             functionName: format.ToString(),
                                             description: $"{coreDescription} Puts the result back on the clipboard.",
                                             parameters: null,
                                             returnParameter);

        return standardTransformFunctions.Prepend(customTextTransformFunction);
    }

    private Task<string> ExecuteCustomTextTransformAsync(Kernel kernel, string inputInstructions) =>
       ExecuteTransformAsync(
           kernel,
           PasteFormats.CustomTextTransformation,
           async dataPackageView =>
           {
               var input = await dataPackageView.GetTextAsync();
               var output = await _customTextTransformService.TransformTextAsync(inputInstructions, input);
               return DataPackageHelpers.CreateFromText(output);
           });

    private Task<string> ExecuteStandardTransformAsync(Kernel kernel, PasteFormats format) =>
        ExecuteTransformAsync(
           kernel,
           format,
           async dataPackageView => await TransformHelpers.TransformAsync(format, dataPackageView));

    private static async Task<string> ExecuteTransformAsync(Kernel kernel, PasteFormats format, Func<DataPackageView, Task<DataPackage>> transformFunc)
    {
        kernel.GetActionChain().Add(format);
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

    private static string FormatChatHistory(ChatHistory chatHistory) => string.Join(Environment.NewLine, chatHistory.Select(FormatChatMessage));

    private static string FormatChatMessage(ChatMessageContent chatMessage)
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
        return $"-> {role}: {redactedContent}";
    }
}
