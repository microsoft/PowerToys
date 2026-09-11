// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class ProgramTests
{
    [TestMethod]
    public async Task StdinToStdout_TransformsJson()
    {
        var result = await RunAsync(["transform", "--format", "json", "--stdin"], "name,age\r\nAda,37");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Stdout, "\"Ada\"");
        Assert.AreEqual(string.Empty, result.Stderr);
    }

    [TestMethod]
    public async Task FileInputAndOutput_TransformsMarkdown()
    {
        var inputPath = Path.GetTempFileName();
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
        var result = await RunAsync(["transform", "--format", "plain-text", "--clipboard", "--output-clipboard"], clipboard: clipboard);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("text", clipboard.WrittenText);
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
        Assert.AreEqual("invalid_arguments", document.RootElement.GetProperty("code").GetString());
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
    public async Task UnsupportedFormat_ReturnsStableJsonError()
    {
        var result = await RunAsync(["transform", "--format", "ocr", "--stdin", "--json"]);

        Assert.AreEqual(2, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stderr);
        Assert.AreEqual("unsupported_format", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task JsonSuccess_IsParseableAndStable()
    {
        var result = await RunAsync(["transform", "--format", "plain-text", "--stdin", "--json"], "hello");

        Assert.AreEqual(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.AreEqual("success", document.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("plain-text", document.RootElement.GetProperty("format").GetString());
        Assert.AreEqual("hello", document.RootElement.GetProperty("output").GetString());
    }

    [TestMethod]
    public async Task Format_IsCaseInsensitive()
    {
        var result = await RunAsync(["transform", "--format", "JSON", "--stdin"], "hello");

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

    private static async Task<RunResult> RunAsync(
        string[] args,
        string input = "",
        TestClipboardAdapter? clipboard = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await Program.RunAsync(args, new StringReader(input), stdout, stderr, clipboard ?? new TestClipboardAdapter(), cancellationToken);
        return new RunResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record RunResult(int ExitCode, string Stdout, string Stderr);

    private sealed class TestClipboardAdapter : IClipboardAdapter
    {
        public TestClipboardAdapter(string text = "")
        {
            Text = text;
        }

        public string Text { get; }

        public string? WrittenText { get; private set; }

        public string ReadText() => Text;

        public void WriteText(string text) => WrittenText = text;
    }
}
