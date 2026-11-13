// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;

using AdvancedPaste.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public class HeadlessTransformServiceTests
{
    [TestMethod]
    public void PlainText_PreservesContent()
        => Assert.AreEqual("hello", HeadlessTransformService.Transform(HeadlessTransformFormat.PlainText, "hello", TestContext.CancellationToken));

    [TestMethod]
    public void Markdown_ConvertsHtmlAndRemovesScripts()
    {
        var result = HeadlessTransformService.Transform(
            HeadlessTransformFormat.Markdown,
            "<p style=\"color:red\">Hello <strong>world</strong></p><script>ignored</script>",
            TestContext.CancellationToken);

        Assert.Contains("Hello **world**", result);
        Assert.IsFalse(result.Contains("ignored", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void Json_ConvertsCsv()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "name,age\r\nAda,37", TestContext.CancellationToken);

        Assert.Contains("\"name\"", result);
        Assert.Contains("\"Ada\"", result);
    }

    [TestMethod]
    public void Json_ConvertsLfDelimitedCsv()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "name,age\nAda,37", TestContext.CancellationToken);

        Assert.Contains("\"Ada\"", result);
        Assert.IsFalse(result.Contains("age\\nAda", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void Json_ConvertsCommaDenseCsvInLinearTime()
    {
        var input = string.Join(',', Enumerable.Repeat("value", 10_000));

        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, input, TestContext.CancellationToken);

        Assert.Contains("\"value\"", result);
    }

    [TestMethod]
    public void Json_ConvertsQuotedCsvDelimiterAndEscapedQuotes()
    {
        var result = HeadlessTransformService.Transform(
            HeadlessTransformFormat.Json,
            "name,note\nAda,\"Hello, \"\"PowerToys\"\"\"",
            TestContext.CancellationToken);

        Assert.Contains("\"Hello, \\\"PowerToys\\\"\"", result);
    }

    [TestMethod]
    public void Json_UsesCustomCsvSeparatorAsLiteral()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "sep=|\nname|age\nAda|37", TestContext.CancellationToken);

        Assert.Contains("\"Ada\"", result);
        Assert.Contains("\"37\"", result);
    }

    [TestMethod]
    public void Json_ConvertsLongUnstructuredInput()
    {
        var input = new string('x', 100_000);

        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, input, TestContext.CancellationToken);

        Assert.IsTrue(result.StartsWith('['));
        Assert.Contains(input, result);
    }

    [TestMethod]
    public void Json_ConvertsXml()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "<note><title>Hello</title></note>", TestContext.CancellationToken);

        Assert.Contains("\"note\"", result);
        Assert.Contains("\"title\": \"Hello\"", result);
    }

    [TestMethod]
    public void Json_ConvertsIni()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "[section]\nname=value", TestContext.CancellationToken);

        Assert.Contains("\"section\"", result);
        Assert.Contains("\"name\": \"value\"", result);
    }

    [TestMethod]
    public void Json_PreservesJson()
        => Assert.AreEqual("{\"name\":\"Ada\"}", HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "{\"name\":\"Ada\"}", TestContext.CancellationToken));

    [TestMethod]
    public void Markdown_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(
            () => HeadlessTransformService.Transform(HeadlessTransformFormat.Markdown, "<p>text</p>", cancellation.Token));
    }

    [TestMethod]
    public void Json_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(
            () => HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "name,value", cancellation.Token));
    }

    public TestContext TestContext { get; set; } = null!;
}
