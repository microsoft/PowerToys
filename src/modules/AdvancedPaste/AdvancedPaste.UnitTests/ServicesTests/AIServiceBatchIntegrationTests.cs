// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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

/// <summary>
/// Tests that write batch AI outputs against a list of inputs. Connects to OpenAI and uses the full AdvancedPaste action catalog for Semantic Kernel.
/// If queries produce errors, the error message is written to the output file. If queries produce text-file output, their contents are included as though they were text output.
/// To run this test-suite, first:
/// 1. Setup an OpenAI API key using AdvancedPaste Settings.
/// 2. Comment out the [Ignore] attribute above.
/// 3. Ensure the %USERPROFILE% folder contains the required input files (paths are below).
/// These tests are idempotent and resumable, allowing for partial runs and restarts. It's ok to use existing output files as input files - output-related fields will simply be ignored.
/// </summary>
public sealed class AIServiceBatchIntegrationTests
{
    private record class BatchTestInput
    {
        public string Prompt { get; init; }

        public string Clipboard { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Genre { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Category { get; init; }
    }

    private sealed record class BatchTestResult : BatchTestInput
    {
        [JsonPropertyOrder(1)]
        public string Result { get; init; }

        internal BatchTestInput ToInput() => new() { Prompt = Prompt, Clipboard = Clipboard, Genre = Genre, Category = Category, };
    }

    private const string AllTestsFilePath = @"%USERPROFILE%\allAdvancedPasteTests-Input-V2.json";
    private const string FailedTestsFilePath = @"%USERPROFILE%\advanced-paste-failed-tests-only.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    [TestMethod]
    [DataRow(AllTestsFilePath, PasteFormats.CustomTextTransformation)]
    [DataRow(AllTestsFilePath, PasteFormats.KernelQuery)]
    [DataRow(FailedTestsFilePath, PasteFormats.CustomTextTransformation)]
    [DataRow(FailedTestsFilePath, PasteFormats.KernelQuery)]
    public async Task TestGenerateBatchResults(string inputFilePath, PasteFormats format)
    {
        // Load input data.
        var fullInputFilePath = Environment.ExpandEnvironmentVariables(inputFilePath);
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
                var textOutput = await GetTextOutputAsync(input, format);
                results.Add(new() { Prompt = input.Prompt, Clipboard = input.Clipboard, Genre = input.Genre, Category = input.Category, Result = textOutput, });
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
        catch (PasteActionModeratedException)
        {
            return $"Error: {PasteActionModeratedException.ErrorDescription}";
        }
        catch (PasteActionException ex) when (!string.IsNullOrEmpty(ex.AIServiceMessage))
        {
            return $"Error: {ex.AIServiceMessage}";
        }
    }

    private static async Task<DataPackage> GetOutputDataPackageAsync(BatchTestInput batchTestInput, PasteFormats format)
    {
        VaultCredentialsProvider credentialsProvider = new();
        PromptModerationService promptModerationService = new(credentialsProvider);
        NoOpProgress progress = new();
        CustomTextTransformService customTextTransformService = new(credentialsProvider, promptModerationService);

        switch (format)
        {
            case PasteFormats.CustomTextTransformation:
                return DataPackageHelpers.CreateFromText(await customTextTransformService.TransformTextAsync(batchTestInput.Prompt, batchTestInput.Clipboard, CancellationToken.None, progress));

            case PasteFormats.KernelQuery:
                var clipboardData = DataPackageHelpers.CreateFromText(batchTestInput.Clipboard).GetView();
                KernelService kernelService = new(new NoOpKernelQueryCacheService(), credentialsProvider, promptModerationService, customTextTransformService);
                return await kernelService.TransformClipboardAsync(batchTestInput.Prompt, clipboardData, isSavedQuery: false, CancellationToken.None, progress);

            default:
                throw new InvalidOperationException($"Unexpected format {format}");
        }
    }
}
