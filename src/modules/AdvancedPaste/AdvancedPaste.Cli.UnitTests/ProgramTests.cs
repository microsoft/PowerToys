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
using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class ProgramTests
{
    [DataTestMethod]
    [DataRow("--action")]
    [DataRow("--format")]
    public async Task StdinToStdout_TransformsJson(string actionOption)
    {
        var result = await RunAsync(["transform", actionOption, "json", "--stdin", "--stdout"], "name,age\r\nAda,37");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Stdout, "\"Ada\"");
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [DataTestMethod]
    [DataRow("--input", "--output")]
    [DataRow("-i", "-o")]
    public async Task FileInputAndOutput_TransformsMarkdown(string inputOption, string outputOption)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(inputPath, "<p>Hello <strong>world</strong></p>");
            var result = await RunAsync(["transform", "--format", "markdown", inputOption, inputPath, outputOption, outputPath]);

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
    public async Task TextOutput_CreatesMissingParentDirectories()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(outputDirectory, "nested");
        var outputPath = Path.Combine(directory, "result.txt");
        try
        {
            var result = await RunAsync(
                ["transform", "--format", "plain-text", "--stdin", "--output", outputPath],
                "hello");

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("hello", await File.ReadAllTextAsync(outputPath));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
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

    [DataTestMethod]
    [DataRow("json", ".csv", "name,age\r\nAda,37", "\"Ada\"", true)]
    [DataRow("json", ".csv", "name,age\r\nAda,37", "\"Ada\"", false)]
    [DataRow("markdown", ".html", "<p>Hello <strong>world</strong></p>", "Hello **world**", true)]
    [DataRow("markdown", ".html", "<p>Hello <strong>world</strong></p>", "Hello **world**", false)]
    public async Task ClipboardFile_TransformsContentAndPreservesAgedSource(string action, string extension, string text, string expected, bool explicitClipboard)
    {
        var directory = AdvancedPasteTempFileManager.CreateDirectory();
        var path = Path.Combine(directory.FullName, $"clipboard{extension}");
        try
        {
            await File.WriteAllTextAsync(path, text);
            Directory.SetCreationTimeUtc(directory.FullName, DateTime.UtcNow.AddDays(-2));
            var clipboard = new TestClipboardAdapter();
            clipboard.Content.SetStorageItems([await Windows.Storage.StorageFile.GetFileFromPathAsync(path)]);

            var args = new List<string> { "transform", "--action", action, "--stdout" };
            if (explicitClipboard)
            {
                args.Add("--clipboard");
            }

            var result = await RunAsync(args.ToArray(), clipboard: clipboard);

            Assert.AreEqual(0, result.ExitCode, result.Stderr);
            StringAssert.Contains(result.Stdout, expected);
            Assert.AreEqual(string.Empty, result.Stderr);
            Assert.AreEqual(text, await File.ReadAllTextAsync(path));
            Assert.AreEqual(1, clipboard.ReadCount);
            Assert.IsNull(clipboard.WrittenContent);
            Assert.AreEqual(path, (await clipboard.Content.GetView().GetStorageItemsAsync())[0].Path);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
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

    [DataTestMethod]
    [DataRow("--json")]
    [DataRow("--json=true")]
    public async Task ProvidersListJson_ListsConfiguredIdsWithoutReadingClipboard(string jsonOption)
    {
        var clipboard = new TestClipboardAdapter("unused clipboard");
        var runtime = new TestRuntime(providers: [new("provider-id", "local model", "Ollama", "llama3.2:3b", true, true)]);

        var result = await RunAsync(["providers", "list", jsonOption], clipboard: clipboard, runtime: runtime);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, clipboard.ReadCount);
        using var document = JsonDocument.Parse(result.Stdout);
        var provider = document.RootElement[0];
        Assert.AreEqual("provider-id", provider.GetProperty("id").GetString());
        Assert.AreEqual("Ollama", provider.GetProperty("serviceType").GetString());
        Assert.AreEqual("llama3.2:3b", provider.GetProperty("model").GetString());
        Assert.IsTrue(provider.GetProperty("isDefault").GetBoolean());
        Assert.IsTrue(provider.GetProperty("isAllowedByPolicy").GetBoolean());
        Assert.IsFalse(provider.TryGetProperty("apiKey", out _));
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [TestMethod]
    public async Task ProvidersList_ExplainsEmptyConfigurationAndPolicyStatus()
    {
        var empty = await RunAsync(["providers", "list"]);
        Assert.AreEqual(0, empty.ExitCode);
        StringAssert.Contains(empty.Stdout, "PowerToys Settings > Advanced Paste");

        var json = await RunAsync(["providers", "list", "--json"]);
        Assert.AreEqual("[]", json.Stdout.Trim());

        var runtime = new TestRuntime(providers: [new("blocked-id", "model", "OpenAI", "model", true, false)]);
        var blocked = await RunAsync(["providers", "list"], runtime: runtime);
        StringAssert.Contains(blocked.Stdout, "blocked-id");
        StringAssert.Contains(blocked.Stdout, "[default]");
        StringAssert.Contains(blocked.Stdout, "[disabled by policy]");
    }

    [TestMethod]
    public async Task ProvidersCommand_RequiresListAndReportsMatchingUsage()
    {
        var incomplete = await RunAsync(["providers"]);
        Assert.AreEqual(2, incomplete.ExitCode);
        StringAssert.Contains(incomplete.Stderr, "providers list");

        var invalid = await RunAsync(["providers", "list", "--json", "--invalid"]);
        Assert.AreEqual(2, invalid.ExitCode);
        using var document = JsonDocument.Parse(invalid.Stderr);
        StringAssert.Contains(document.RootElement.GetProperty("usage").GetString(), "providers list [--json]");
        Assert.AreEqual(string.Empty, invalid.Stdout);
    }

    [TestMethod]
    public async Task TransformHelp_ExplainsProviderPurposeAndIdDiscovery()
    {
        var result = await RunAsync(["transform", "--help"]);

        Assert.AreEqual(0, result.ExitCode);
        var help = string.Join(" ", result.Stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        StringAssert.Contains(help, "AI actions only");
        StringAssert.Contains(help, "providers list");
        StringAssert.Contains(help, "service name");
    }

    [DataTestMethod]
    [DataRow(new[] { "--help" })]
    [DataRow(new[] { "-h" })]
    [DataRow(new[] { "-?" })]
    [DataRow(new[] { "/?" })]
    [DataRow(new[] { "transform", "--help" })]
    [DataRow(new[] { "actions", "list", "--help" })]
    public async Task Help_DescribesEverySupportedBuiltInAction(string[] args)
    {
        var result = await RunAsync(args, runtime: new TestRuntime(isEnabled: false));

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.Stderr);
        var help = string.Join(" ", result.Stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        foreach (var action in AdvancedPasteRuntime.GetBuiltInActions())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(action.Description), action.Name);
            StringAssert.Contains(help, action.Name);
            StringAssert.Contains(help, action.Description);
        }
    }

    [DataTestMethod]
    [DataRow("plain-text", "provider-id")]
    [DataRow("markdown", "provider-id")]
    [DataRow("json", "provider-id")]
    [DataRow("fix-spelling-and-grammar", "")]
    public async Task ProviderOption_RejectsNonAiActionsAndEmptyIdsBeforeClipboardRead(string action, string provider)
    {
        var clipboard = new TestClipboardAdapter("unused");
        var result = await RunAsync(
            ["transform", "--action", action, "--provider", provider, "--json"],
            clipboard: clipboard);

        Assert.AreEqual(2, result.ExitCode);
        Assert.AreEqual(0, clipboard.ReadCount);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("invalid_provider", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task ActionsListJson_ExplicitTrue_IsMachineReadable()
    {
        var result = await RunAsync(["actions", "list", "--json=true"]);

        Assert.AreEqual(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.AreEqual("plain-text", document.RootElement[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task TransformJson_ExplicitFalse_UsesHumanReadableOutput()
    {
        var result = await RunAsync(
            ["transform", "--format", "plain-text", "--stdin", "--stdout", "--json", "false"],
            "hello");

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("hello", result.Stdout);
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [TestMethod]
    public async Task ActionsParentCommand_RequiresListSubcommand()
    {
        var result = await RunAsync(["actions"]);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "actions list");
    }

    [TestMethod]
    public async Task ActionsListJson_ParseErrorIsMachineReadable()
    {
        var result = await RunAsync(["actions", "list", "--json", "--invalid"]);

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("error", document.RootElement.GetProperty("status").GetString());
        StringAssert.Contains(document.RootElement.GetProperty("usage").GetString(), "actions list [--json]");
        Assert.IsFalse(document.RootElement.GetProperty("usage").GetString()!.Contains(" transform ", StringComparison.Ordinal));
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

    [DataTestMethod]
    [DataRow(new[] { "--stdin", "--clipboard" })]
    [DataRow(new[] { "--input", "unused.csv", "--clipboard" })]
    [DataRow(new[] { "--input", "unused.csv", "--stdin" })]
    public async Task ConflictingInputModes_ReturnArgumentError(string[] inputOptions)
    {
        var clipboard = new TestClipboardAdapter("do not read");
        var result = await RunAsync(["transform", "--format", "json", "--json", .. inputOptions], clipboard: clipboard);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "invalid_input_mode");
        Assert.AreEqual(0, clipboard.ReadCount);
    }

    [TestMethod]
    public async Task ConflictingOutputModes_ReturnArgumentError()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin", "--stdout", "--output-clipboard", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(result.Stderr, "invalid_");
    }

    [TestMethod]
    public async Task DefaultInput_EmptyCopiedFileReturnsEmptyInputError()
    {
        var path = Path.GetTempFileName();
        try
        {
            var clipboard = new TestClipboardAdapter();
            clipboard.Content.SetStorageItems([await Windows.Storage.StorageFile.GetFileFromPathAsync(path)]);

            var result = await RunAsync(["transform", "--action", "json", "--stdout", "--json"], clipboard: clipboard);

            Assert.AreEqual(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.Stderr);
            Assert.AreEqual("empty_input", document.RootElement.GetProperty("code").GetString());
            Assert.IsNull(clipboard.WrittenContent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task DefaultInput_EmptyClipboardReturnsRuntimeError()
    {
        var clipboard = new TestClipboardAdapter();
        var result = await RunAsync(["transform", "--format", "json", "--stdout", "--json"], clipboard: clipboard);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(1, clipboard.ReadCount);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("empty_input", document.RootElement.GetProperty("code").GetString());
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DefaultInput_UsesClipboardEvenWithRedirectedStdin(bool stdout)
    {
        var clipboard = new TestClipboardAdapter("clipboard input");
        var args = new List<string> { "transform", "--action", "plain-text" };
        if (stdout)
        {
            args.Add("--stdout");
        }

        var result = await RunAsync(args.ToArray(), "unused stdin", clipboard);

        Assert.AreEqual(0, result.ExitCode, result.Stderr);
        Assert.AreEqual(1, clipboard.ReadCount);
        if (stdout)
        {
            Assert.AreEqual("clipboard input", result.Stdout);
            Assert.IsNull(clipboard.WrittenContent);
        }
        else
        {
            Assert.AreEqual(string.Empty, result.Stdout);
            Assert.AreEqual("clipboard input", await clipboard.WrittenContent!.GetView().GetTextAsync());
        }
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

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
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
    public async Task FileOutput_CancellationCleansGeneratedSource()
    {
        var sourceDirectory = AdvancedPasteTempFileManager.CreateDirectory();
        var sourcePath = Path.Combine(sourceDirectory.FullName, "generated.txt");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(sourcePath, "generated content");
        var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
        var package = new DataPackage();
        package.SetStorageItems([source]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                CliOutputWriter.WriteAsync(
                    package,
                    new FileInfo(outputPath),
                    stdoutRequested: false,
                    clipboard: new TestClipboardAdapter(),
                    stdout: TextWriter.Null,
                    cancellationToken: cancellation.Token));

            Assert.IsFalse(Directory.Exists(sourceDirectory.FullName));
        }
        finally
        {
            if (Directory.Exists(sourceDirectory.FullName))
            {
                Directory.Delete(sourceDirectory.FullName, recursive: true);
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [TestMethod]
    public async Task FileOutputToStdout_CleansGeneratedSource()
    {
        var sourceDirectory = AdvancedPasteTempFileManager.CreateDirectory();
        var sourcePath = Path.Combine(sourceDirectory.FullName, "generated.txt");
        await File.WriteAllTextAsync(sourcePath, "generated content");
        var source = await Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
        var package = new DataPackage();
        package.SetStorageItems([source]);

        try
        {
            await Assert.ThrowsExactlyAsync<UnsupportedOutputException>(() =>
                CliOutputWriter.WriteAsync(
                    package,
                    outputFile: null,
                    stdoutRequested: true,
                    clipboard: new TestClipboardAdapter(),
                    stdout: TextWriter.Null,
                    cancellationToken: CancellationToken.None));

            Assert.IsFalse(Directory.Exists(sourceDirectory.FullName));
        }
        finally
        {
            if (Directory.Exists(sourceDirectory.FullName))
            {
                Directory.Delete(sourceDirectory.FullName, recursive: true);
            }
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

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
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
    public async Task ActionUnavailableException_UsesActionUnavailableError()
    {
        var result = await RunAsync(
            ["transform", "--action", "plain-text", "--stdin", "--json"],
            "hello",
            runtime: new ExceptionRuntime(new CliActionUnavailableException("Action is unavailable.")));

        Assert.AreEqual(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("action_unavailable", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task NestedProviderPolicyDenial_UsesStableUnavailableError()
    {
        var result = await RunAsync(
            ["transform", "--action", "paste-with-ai", "--prompt", "prompt", "--stdin", "--stdout", "--json"],
            "input",
            runtime: new ExceptionRuntime(new AdvancedPaste.Services.ProviderPolicyDisabledException()));

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.Stdout);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("action_unavailable", document.RootElement.GetProperty("code").GetString());
        Assert.AreEqual("The selected AI provider is disabled by policy.", document.RootElement.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task InvalidOperationFromTransformation_UsesGenericError()
    {
        var result = await RunAsync(
            ["transform", "--action", "plain-text", "--stdin", "--json"],
            "hello",
            runtime: new ExceptionRuntime(new InvalidOperationException("Internal transform detail.")));

        Assert.AreEqual(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("transformation_error", document.RootElement.GetProperty("code").GetString());
        Assert.IsFalse(document.RootElement.GetProperty("message").GetString()!.Contains("Internal transform detail.", StringComparison.Ordinal));
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
    public void StartupFailure_WithJsonFalse_WritesHumanReadableError()
    {
        var stderr = new StringWriter();

        Program.WriteStartupError(["transform", "--json", "false"], stderr);

        StringAssert.Contains(stderr.ToString(), "Error: Advanced Paste CLI failed.");
    }

    [TestMethod]
    public async Task DisabledByPolicy_WritesStableJsonError()
    {
        var result = await RunAsync(
            ["transform", "--action", "plain-text", "--stdin", "--json"],
            "hello",
            runtime: new ExceptionRuntime(new CliPolicyDisabledException()));

        Assert.AreEqual(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("disabled_by_policy", document.RootElement.GetProperty("code").GetString());
    }

    [DataTestMethod]
    [DataRow(new[] { "actions", "list", "--json" })]
    [DataRow(new[] { "providers", "list", "--json" })]
    [DataRow(new[] { "transform", "--action", "plain-text", "--clipboard", "--json" })]
    [DataRow(new[] { "transform", "--action", "plain-text", "--json" })]
    public async Task DisabledPolicy_BlocksListingAndClipboardRead(string[] args)
    {
        var clipboard = new TestClipboardAdapter("private input");
        var runtime = new TestRuntime(isEnabled: false);
        var result = await RunAsync(args, clipboard: clipboard, runtime: runtime);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(string.Empty, result.Stdout);
        Assert.AreEqual(0, clipboard.ReadCount);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("disabled_by_policy", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public void StartupPolicyGuard_ProducesStableErrorWithoutRuntime()
    {
        var stderr = new StringWriter();

        Assert.IsTrue(Program.TryWritePolicyDisabledError(["actions", "list", "--json"], stderr, policyEnabled: false));
        using var document = JsonDocument.Parse(stderr.ToString());
        Assert.AreEqual("disabled_by_policy", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Help_RemainsAvailableWhenPolicyIsDisabled()
    {
        var result = await RunAsync(["transform", "--help"], runtime: new TestRuntime(isEnabled: false));

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [TestMethod]
    public async Task FileMode_PreservesAgedOwnedInputWithoutReadingClipboard()
    {
        var directory = AdvancedPasteTempFileManager.CreateDirectory();
        var path = Path.Combine(directory.FullName, "input.txt");
        await File.WriteAllTextAsync(path, "preserve input");
        Directory.SetCreationTimeUtc(directory.FullName, DateTime.UtcNow.AddDays(-2));
        var clipboard = new TestClipboardAdapter();
        try
        {
            var result = await RunAsync(["transform", "--action", "plain-text", "--input", path, "--stdout"], clipboard: clipboard);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("preserve input", result.Stdout);
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(0, clipboard.ReadCount);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [TestMethod]
    public async Task ClipboardMode_PreservesAgedClipboardAndOutputDirectories()
    {
        var inputDirectory = AdvancedPasteTempFileManager.CreateDirectory();
        var outputDirectory = AdvancedPasteTempFileManager.CreateDirectory();
        var inputPath = Path.Combine(inputDirectory.FullName, "clipboard.txt");
        var outputPath = Path.Combine(outputDirectory.FullName, "output.txt");
        await File.WriteAllTextAsync(inputPath, "existing clipboard file");
        await File.WriteAllTextAsync(outputPath, "existing output");
        Directory.SetCreationTimeUtc(inputDirectory.FullName, DateTime.UtcNow.AddDays(-2));
        Directory.SetCreationTimeUtc(outputDirectory.FullName, DateTime.UtcNow.AddDays(-2));
        var clipboard = new TestClipboardAdapter("result");
        clipboard.Content.SetStorageItems([await Windows.Storage.StorageFile.GetFileFromPathAsync(inputPath)]);
        try
        {
            var result = await RunAsync(["transform", "--action", "plain-text", "--clipboard", "--output", outputPath], clipboard: clipboard);

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("existing clipboard file", await File.ReadAllTextAsync(inputPath));
            Assert.AreEqual("result", await File.ReadAllTextAsync(outputPath));
        }
        finally
        {
            Directory.Delete(inputDirectory.FullName, recursive: true);
            Directory.Delete(outputDirectory.FullName, recursive: true);
        }
    }

    [TestMethod]
    public void LoggerInitializationFailure_DoesNotPreventCliStartup()
    {
        Assert.IsFalse(Program.TryInitializeLogger(() => throw new UnauthorizedAccessException()));
    }

    [TestMethod]
    public void CreateUtf8InputReader_PreservesRedirectedUnicodeInput()
    {
        const string input = "こんにちは PowerToys";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
        using var reader = Program.CreateUtf8InputReader(stream);

        Assert.AreEqual(input, reader.ReadToEnd());
    }

    [DataTestMethod]
    [DataRow(new string[] { }, "help")]
    [DataRow(new[] { "transform", "--help" }, "help")]
    [DataRow(new[] { "transform", "--action", "plain-text", "--input", "-h", "--stdout" }, "transform")]
    [DataRow(new[] { "transform", "--input", "-h", "--invalid" }, "transform")]
    [DataRow(new[] { "transform", "--action", "plain-text" }, "transform")]
    [DataRow(new[] { "actions", "list", "--json" }, "actions list")]
    [DataRow(new[] { "providers", "list", "--json" }, "providers list")]
    [DataRow(new[] { "unexpected", "--prompt", "sensitive prompt" }, "unknown")]
    public void TelemetryCommandName_IsBounded(string[] args, string expected)
    {
        Assert.AreEqual(expected, Program.GetTelemetryCommandName(args));
    }

    [TestMethod]
    public void CreateCLITelemetryEvent_SetsBoundedCommandAndSuccessStatus()
    {
        var telemetryEvent = Program.CreateCLITelemetryEvent("transform", successful: true);

        Assert.AreEqual("AdvancedPaste_CLICommand", telemetryEvent.EventName);
        Assert.AreEqual("transform", telemetryEvent.CommandName);
        Assert.IsTrue(telemetryEvent.Successful);
    }

    [TestMethod]
    public async Task HelpLikeOptionValue_WithParseError_ReturnsArgumentError()
    {
        var result = await RunAsync(["transform", "--input", "-h", "--invalid"]);

        Assert.AreEqual(Program.ArgumentErrorExitCode, result.ExitCode);
        StringAssert.Contains(result.Stderr, "Usage:");
    }

    [DataTestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    [DataRow("-?")]
    [DataRow("/?")]
    public async Task HelpLikePromptValue_ExecutesActionInsteadOfShowingHelp(string prompt)
    {
        var result = await RunAsync(
            ["transform", "--action", "paste-with-ai", "--prompt", prompt, "--stdin", "--stdout"],
            "hello");

        Assert.AreEqual(Program.SuccessExitCode, result.ExitCode);
        Assert.AreEqual("hello", result.Stdout);
        Assert.AreEqual(string.Empty, result.Stderr);
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
        public bool IsEnabled => true;

        public IReadOnlyList<CliActionDescriptor> GetActions() => [];

        public IReadOnlyList<CliProviderDescriptor> GetProviders() => [];

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

        public int ReadCount { get; private set; }

        public DataPackageView Read()
        {
            ReadCount++;
            return Content.GetView();
        }

        public void Write(DataPackage content) => WrittenContent = content;
    }

    private sealed class TestRuntime(bool isEnabled = true, IReadOnlyList<CliProviderDescriptor>? providers = null) : IAdvancedPasteRuntime
    {
        public bool IsEnabled => isEnabled;

        public IReadOnlyList<CliActionDescriptor> GetActions()
            => [new("plain-text", "built-in", null, RequiresPrompt: false)];

        public IReadOnlyList<CliProviderDescriptor> GetProviders() => providers ?? [];

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
        public bool IsEnabled => true;

        public string OutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<CliActionDescriptor> GetActions() => [];

        public IReadOnlyList<CliProviderDescriptor> GetProviders() => [];

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
