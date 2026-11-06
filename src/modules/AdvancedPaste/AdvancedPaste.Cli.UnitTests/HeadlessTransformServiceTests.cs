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
public class HeadlessTransformServiceTests
{
    [TestMethod]
    public void PlainText_PreservesContent()
        => Assert.AreEqual("hello", HeadlessTransformService.Transform(HeadlessTransformFormat.PlainText, "hello"));

    [TestMethod]
    public void Markdown_ConvertsHtmlAndRemovesScripts()
    {
        var result = HeadlessTransformService.Transform(
            HeadlessTransformFormat.Markdown,
            "<p style=\"color:red\">Hello <strong>world</strong></p><script>ignored</script>");

        StringAssert.Contains(result, "Hello **world**");
        Assert.IsFalse(result.Contains("ignored", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void Json_ConvertsCsv()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "name,age\r\nAda,37");

        StringAssert.Contains(result, "\"name\"");
        StringAssert.Contains(result, "\"Ada\"");
    }

    [TestMethod]
    public void Json_ConvertsLfDelimitedCsv()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "name,age\nAda,37");

        StringAssert.Contains(result, "\"Ada\"");
        Assert.IsFalse(result.Contains("age\\nAda", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void Json_ConvertsCommaDenseCsvInLinearTime()
    {
        var input = string.Join(',', Enumerable.Repeat("value", 10_000));

        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, input);

        StringAssert.Contains(result, "\"value\"");
    }

    [TestMethod]
    public void Json_ConvertsQuotedCsvDelimiterAndEscapedQuotes()
    {
        var result = HeadlessTransformService.Transform(
            HeadlessTransformFormat.Json,
            "name,note\nAda,\"Hello, \"\"PowerToys\"\"\"");

        StringAssert.Contains(result, "\"Hello, \\\"PowerToys\\\"\"");
    }

    [TestMethod]
    public void Json_UsesCustomCsvSeparatorAsLiteral()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "sep=|\nname|age\nAda|37");

        StringAssert.Contains(result, "\"Ada\"");
        StringAssert.Contains(result, "\"37\"");
    }

    [TestMethod]
    public void Json_ConvertsLongUnstructuredInput()
    {
        var input = new string('x', 100_000);

        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, input);

        Assert.IsTrue(result.StartsWith('['));
        StringAssert.Contains(result, input);
    }

    [TestMethod]
    public void Json_ConvertsXml()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "<note><title>Hello</title></note>");

        StringAssert.Contains(result, "\"note\"");
        StringAssert.Contains(result, "\"title\": \"Hello\"");
    }

    [TestMethod]
    public void Json_ConvertsIni()
    {
        var result = HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "[section]\nname=value");

        StringAssert.Contains(result, "\"section\"");
        StringAssert.Contains(result, "\"name\": \"value\"");
    }

    [TestMethod]
    public void Json_PreservesJson()
        => Assert.AreEqual("{\"name\":\"Ada\"}", HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "{\"name\":\"Ada\"}"));

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
}
