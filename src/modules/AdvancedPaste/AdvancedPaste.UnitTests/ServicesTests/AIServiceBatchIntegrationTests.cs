// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services.OpenAI;
using AdvancedPaste.UnitTests.Mocks;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests;

[Ignore("Test requires active OpenAI API key.")] // Comment out this line to run these tests after setting up OpenAI API key using AdvancedPaste Settings
[TestClass]

/// <summary>Batch integration tests for the AI services; connects to OpenAI and uses full AdvancedPaste action catalog for Semantic Kernel.</summary>
public sealed class AIServiceBatchIntegrationTests
{
    private sealed record class BatchTestInput(string Prompt, string Clipboard, string Genre);

    private sealed record class BatchTestResult(string Prompt, string Clipboard, string Genre, string Result)
    {
        internal BatchTestInput ToInput() => new(Prompt, Clipboard, Genre);
    }

    private const string InputFilePath = @"%USERPROFILE%\allAdvancedPasteTests-Input-V2.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    [TestMethod]
    [DataRow(PasteFormats.CustomTextTransformation)]
    [DataRow(PasteFormats.KernelQuery)]
    public async Task TestGenerateBatchResults(PasteFormats format)
    {
        // Load input data.
        var fullInputFilePath = Environment.ExpandEnvironmentVariables(InputFilePath);
        var inputs = await GetDataListAsync<BatchTestInput>(fullInputFilePath);
        Assert.IsTrue(inputs.Count > 0);

        // Load existing results; allow a partial run to be resumed.
        var resultsFile = Path.Combine(Path.GetDirectoryName(fullInputFilePath), $"{Path.GetFileNameWithoutExtension(fullInputFilePath)}-output-{format}.json");
        var results = await GetDataListAsync<BatchTestResult>(resultsFile);
        Assert.IsTrue(results.Count <= inputs.Count);
        CollectionAssert.AreEqual(results.Select(result => result.ToInput()).ToList(), inputs.Take(results.Count).ToList());

        async Task WriteResultsAsync() => await File.WriteAllTextAsync(resultsFile, JsonSerializer.Serialize(results, SerializerOptions));

        Logger.LogInfo($"Starting {nameof(TestGenerateBatchResults)}; Count={inputs.Count}, InCache={results.Count}");

        // Produce results for any unprocessed inputs.
        foreach (var input in inputs.Skip(results.Count))
        {
            try
            {
                var output = await GetTextOutputAsync(input, format);
                results.Add(new BatchTestResult(input.Prompt, input.Clipboard, input.Genre, output));
            }
            catch (Exception)
            {
                await WriteResultsAsync();
                throw;
            }
        }

        await WriteResultsAsync();
    }

    private static async Task<List<T>> GetDataListAsync<T>(string filePath) =>
        File.Exists(filePath) ? JsonSerializer.Deserialize<List<T>>(await File.ReadAllTextAsync(filePath)) : [];

    private static async Task<string> GetTextOutputAsync(BatchTestInput input, PasteFormats format)
    {
        try
        {
            var outputPackage = (await GetOutputDataPackageAsync(input, format)).GetView();
            var outputFormat = await outputPackage.GetAvailableFormatsAsync();

            return outputFormat switch
            {
                ClipboardFormat.Text => await outputPackage.GetTextOrEmptyAsync(),
                ClipboardFormat.File => await File.ReadAllTextAsync((await outputPackage.GetStorageItemsAsync()).Single().Path),
                _ => throw new InvalidOperationException($"Unexpected format {outputFormat}"),
            };
        }
        catch (PasteActionException ex) when (!string.IsNullOrEmpty(ex.AIServiceMessage))
        {
            return $"Error: {ex.AIServiceMessage}";
        }
    }

    private static async Task<DataPackage> GetOutputDataPackageAsync(BatchTestInput batchTestInput, PasteFormats format)
    {
        VaultCredentialsProvider aiCredentialsProvider = new();
        CustomTextTransformService customTextTransformService = new(aiCredentialsProvider);

        switch (format)
        {
            case PasteFormats.CustomTextTransformation:
                return DataPackageHelpers.CreateFromText(await customTextTransformService.TransformTextAsync(batchTestInput.Prompt, batchTestInput.Clipboard));

            case PasteFormats.KernelQuery:
                var clipboardData = DataPackageHelpers.CreateFromText(batchTestInput.Clipboard).GetView();
                KernelService kernelService = new(new NoOpKernelQueryCacheService(), aiCredentialsProvider, customTextTransformService);
                return await kernelService.TransformClipboardAsync(batchTestInput.Prompt, clipboardData, isSavedQuery: false);

            default:
                throw new InvalidOperationException($"Unexpected format {format}");
        }
    }
}
