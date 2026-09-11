// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public void Json_PreservesJson()
        => Assert.AreEqual("{\"name\":\"Ada\"}", HeadlessTransformService.Transform(HeadlessTransformFormat.Json, "{\"name\":\"Ada\"}"));
}
