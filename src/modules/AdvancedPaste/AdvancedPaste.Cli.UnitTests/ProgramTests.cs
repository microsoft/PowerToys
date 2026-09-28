// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Cli;
using AdvancedPaste.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class ProgramTests
{
    [TestMethod]
    public async Task StdinToStdout_TransformsJson()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin", "--stdout"], "name,age\r\nAda,37");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Stdout, "\"Ada\"");
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [TestMethod]
    public async Task FileInputAndOutput_TransformsMarkdown()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(inputPath, "<p>Hello <strong>world</strong></p>");
            var result = await RunAsync(["transform", "--format", "markdown", "--input", inputPath, "--output", outputPath]);

            Assert.AreEqual(0, result.ExitCode);
            StringAssert.Contains(await File.ReadAllTextAsync(outputPath), "Hello **world**");
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    [TestMethod]
    public async Task ClipboardInputAndOutput_UsesClipboardAdapter()
    {
        var clipboard = new TestClipboardAdapter("text");
        var result = await RunAsync(["transform", "--format", "plain-text", "--clipboard"], clipboard: clipboard);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("text", await clipboard.WrittenContent!.GetView().GetTextAsync());
    }

    [TestMethod]
    public async Task DefaultOutput_WritesClipboardAndNotStdout()
    {
        var clipboard = new TestClipboardAdapter();
        var result = await RunAsync(["transform", "--action", "plain-text", "--stdin"], "hello", clipboard);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.Stdout);
        Assert.AreEqual("hello", await clipboard.WrittenContent!.GetView().GetTextAsync());
    }

    [TestMethod]
    public async Task PasteWithAi_RequiresPrompt()
    {
        var result = await RunAsync(["transform", "--action", "paste-with-ai", "--stdin"], "hello");

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "prompt");
    }

    [TestMethod]
    public async Task ActionsListJson_IsMachineReadable()
    {
        var result = await RunAsync(["actions", "list", "--json"]);

        Assert.AreEqual(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.AreEqual("plain-text", document.RootElement[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task ActionsListJson_ParseErrorIsMachineReadable()
    {
        var result = await RunAsync(["actions", "list", "--json", "--invalid"]);

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("error", document.RootElement.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task PasteWithAi_WithPromptReachesRuntime()
    {
        var result = await RunAsync(["transform", "--action", "paste-with-ai", "--prompt", "Summarize", "--stdin", "--stdout"], "hello");

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("hello", result.Stdout);
    }

    [TestMethod]
    public async Task SavedCustomAction_ReachesRuntime()
    {
        var result = await RunAsync(["transform", "--custom-action", "7", "--stdin", "--stdout"], "hello");

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("hello", result.Stdout);
    }

    [TestMethod]
    public async Task FileResultToStdout_ReturnsRuntimeError()
    {
        var runtime = new FileResultRuntime();
        var result = await RunAsync(
            ["transform", "--action", "paste-as-txt-file", "--stdin", "--stdout", "--json"],
            "hello",
            runtime: runtime);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Stderr, "unsupported_output");
        File.Delete(runtime.OutputPath);
    }

    [TestMethod]
    public async Task ConflictingInputModes_ReturnArgumentError()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin", "--clipboard", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "invalid_");
    }

    [TestMethod]
    public async Task ConflictingOutputModes_ReturnArgumentError()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin", "--stdout", "--output-clipboard", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "invalid_");
    }

    [TestMethod]
    public async Task MissingInputMode_ReturnsArgumentErrorAndUsage()
    {
        var result = await RunAsync(["transform", "--format", "json"]);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "Usage:");
    }

    [TestMethod]
    public async Task MissingFormat_ReturnsArgumentError()
    {
        var result = await RunAsync(["transform", "--stdin", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("invalid_action", document.RootElement.GetProperty("code").GetString());
        StringAssert.Contains(document.RootElement.GetProperty("usage").GetString(), "PowerToys.AdvancedPaste.CLI.exe transform");
    }

    [TestMethod]
    public async Task MissingFile_ReturnsRuntimeError()
    {
        var result = await RunAsync(["transform", "--format", "json", "--input", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "--json"]);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Stderr, "io_error");
    }

    [TestMethod]
    public async Task EmptyInput_ReturnsRuntimeError()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin", "--json"]);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Stderr, "empty_input");
    }

    [TestMethod]
    public async Task EmptyHtmlFile_ReturnsRuntimeError()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(inputPath, string.Empty);
        try
        {
            var result = await RunAsync(["transform", "--format", "plain-text", "--input", inputPath, "--json"]);

            Assert.AreEqual(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.Stderr);
            Assert.AreEqual("empty_input", document.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [TestMethod]
    public async Task OversizedClipboardInput_ReturnsSpecificRuntimeError()
    {
        var clipboard = new TestClipboardAdapter(new string('x', (16 * 1024 * 1024) + 1));
        var result = await RunAsync(["transform", "--format", "plain-text", "--clipboard", "--json"], clipboard: clipboard);

        Assert.AreEqual(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("input_too_large", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task DirectoryOutput_ReturnsIoError()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var result = await RunAsync(["transform", "--format", "json", "--stdin", "--output", outputDirectory, "--json"], "hello");

            Assert.AreEqual(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.Stderr);
            Assert.AreEqual("io_error", document.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            Directory.Delete(outputDirectory);
        }
    }

    [TestMethod]
    public async Task FileOutput_CancellationKeepsExistingDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source.txt");
        var outputPath = Path.Combine(directory, "output.txt");
        await File.WriteAllTextAsync(sourcePath, "generated content");
        await File.WriteAllTextAsync(outputPath, "existing content");

        try
        {
            var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var package = new DataPackage();
            package.SetStorageItems([source]);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                CliOutputWriter.WriteAsync(
                    package,
                    new FileInfo(outputPath),
                    stdoutRequested: false,
                    clipboard: new TestClipboardAdapter(),
                    stdout: TextWriter.Null,
                    cancellationToken: cancellation.Token));

            Assert.AreEqual("existing content", await File.ReadAllTextAsync(outputPath));
            Assert.AreEqual(0, Directory.GetFiles(directory, ".output.txt.*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task TextFileOutput_CancellationKeepsExistingDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "output.txt");
        await File.WriteAllTextAsync(outputPath, "existing content");

        try
        {
            var package = new DataPackage();
            package.SetText("replacement content");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                CliOutputWriter.WriteAsync(
                    package,
                    new FileInfo(outputPath),
                    stdoutRequested: false,
                    clipboard: new TestClipboardAdapter(),
                    stdout: TextWriter.Null,
                    cancellationToken: cancellation.Token));

            Assert.AreEqual("existing content", await File.ReadAllTextAsync(outputPath));
            Assert.AreEqual(0, Directory.GetFiles(directory, ".output.txt.*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ClipboardOutput_CancellationDoesNotReplaceClipboard()
    {
        var clipboard = new TestClipboardAdapter();
        var package = new DataPackage();
        package.SetText("replacement content");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            CliOutputWriter.WriteAsync(
                package,
                outputFile: null,
                stdoutRequested: false,
                clipboard,
                TextWriter.Null,
                cancellation.Token));

        Assert.IsNull(clipboard.WrittenContent);
    }

    [TestMethod]
    public async Task ClipboardOutput_RejectsEmptyResult()
    {
        var clipboard = new TestClipboardAdapter();
        var package = new DataPackage();
        package.SetText(string.Empty);

        await Assert.ThrowsExactlyAsync<UnsupportedOutputException>(() =>
            CliOutputWriter.WriteAsync(
                package,
                outputFile: null,
                stdoutRequested: false,
                clipboard,
                TextWriter.Null,
                CancellationToken.None));

        Assert.IsNull(clipboard.WrittenContent);
    }

    [TestMethod]
    public async Task UnsupportedFormat_ReturnsStableJsonError()
    {
        var result = await RunAsync(["transform", "--format", "ocr", "--stdin", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("unsupported_action", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task JsonSuccess_IsParseableAndStable()
    {
        var result = await RunAsync(["transform", "--format", "plain-text", "--stdin", "--json"], "hello");

        Assert.AreEqual(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.AreEqual("success", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("plain-text", document.RootElement.GetProperty("action").GetString());
        Assert.AreEqual("hello", document.RootElement.GetProperty("output").GetString());
    }

    [TestMethod]
    public async Task Format_IsCaseInsensitive()
    {
        var result = await RunAsync(["transform", "--format", "JSON", "--stdin", "--stdout"], "hello");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Stdout, "hello");
    }

    [TestMethod]
    public async Task Cancellation_ReturnsRuntimeError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await RunAsync(["transform", "--format", "json", "--stdin"], "hello", cancellationToken: cancellation.Token);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Stderr, "cancelled");
    }

    [TestMethod]
    public async Task ArgumentExceptionFromTransformation_UsesRuntimeExitCode()
    {
        var result = await RunAsync(
            ["transform", "--action", "plain-text", "--stdin", "--json"],
            "hello",
            runtime: new ExceptionRuntime(new ArgumentException("Transformation input was invalid.")));

        Assert.AreEqual(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("transformation_error", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task ActionResolutionException_UsesArgumentExitCode()
    {
        var result = await RunAsync(
            ["transform", "--action", "plain-text", "--stdin", "--json"],
            "hello",
            runtime: new ExceptionRuntime(new CliActionResolutionException("Custom action was not found.")));

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("invalid_action", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public void StartupFailure_WithJsonOption_WritesJsonErrorEnvelope()
    {
        var stderr = new StringWriter();

        Program.WriteStartupError(["transform", "--json"], stderr);

        using var document = JsonDocument.Parse(stderr.ToString());
        Assert.AreEqual("internal_error", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public void DisabledByPolicy_WritesStableJsonError()
    {
        var stderr = new StringWriter();

        Assert.IsTrue(Program.TryWritePolicyDisabledError(isEnabledByPolicy: false, ["transform", "--json"], stderr));

        using var document = JsonDocument.Parse(stderr.ToString());
        Assert.AreEqual("disabled_by_policy", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<RunResult> RunAsync(
        string[] args,
        string input = "",
        TestClipboardAdapter? clipboard = null,
        IAdvancedPasteRuntime? runtime = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await Program.RunAsync(args, new StringReader(input), stdout, stderr, clipboard ?? new TestClipboardAdapter(), runtime ?? new TestRuntime(), cancellationToken);
        return new RunResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record RunResult(int ExitCode, string Stdout, string Stderr);

    private sealed class ExceptionRuntime(Exception exception) : IAdvancedPasteRuntime
    {
        public IReadOnlyList<CliActionDescriptor> GetActions() => [];

        public Task<DataPackage> ExecuteAsync(CliActionRequest request, DataPackageView input, CancellationToken cancellationToken, IProgress<double>? progress = null)
            => Task.FromException<DataPackage>(exception);
    }

    private sealed class TestClipboardAdapter : IClipboardAdapter
    {
        public TestClipboardAdapter(string text = "")
        {
            var package = new DataPackage();
            if (!string.IsNullOrEmpty(text))
            {
                package.SetText(text);
            }

            Content = package;
        }

        public DataPackage Content { get; }

        public DataPackage? WrittenContent { get; private set; }

        public DataPackageView Read() => Content.GetView();

        public void Write(DataPackage content) => WrittenContent = content;
    }

    private sealed class TestRuntime : IAdvancedPasteRuntime
    {
        public IReadOnlyList<CliActionDescriptor> GetActions()
            => [new("plain-text", "built-in", null, RequiresPrompt: false)];

        public async Task<DataPackage> ExecuteAsync(CliActionRequest request, DataPackageView input, CancellationToken cancellationToken, IProgress<double>? progress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = input.Contains(StandardDataFormats.Html) && string.Equals(request.Action, "markdown", StringComparison.OrdinalIgnoreCase)
                ? await input.GetHtmlFormatAsync()
                : await input.GetTextAsync();
            var format = request.CustomAction is not null || string.Equals(request.Action, "paste-with-ai", StringComparison.OrdinalIgnoreCase)
                ? HeadlessTransformFormat.PlainText
                : request.Action?.ToLowerInvariant() switch
            {
                "plain-text" => HeadlessTransformFormat.PlainText,
                "markdown" => HeadlessTransformFormat.Markdown,
                "json" => HeadlessTransformFormat.Json,
                _ => throw new ArgumentException("Unsupported test action."),
            };

            var output = new DataPackage();
            output.SetText(HeadlessTransformService.Transform(format, text, cancellationToken));
            return output;
        }
    }

    private sealed class FileResultRuntime : IAdvancedPasteRuntime
    {
        public string OutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<CliActionDescriptor> GetActions() => [];

        public async Task<DataPackage> ExecuteAsync(CliActionRequest request, DataPackageView input, CancellationToken cancellationToken, IProgress<double>? progress = null)
        {
            OutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(OutputPath, await input.GetTextAsync(), cancellationToken);
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(OutputPath);
            var output = new DataPackage();
            output.SetStorageItems([file]);
            return output;
        }
    }
}
