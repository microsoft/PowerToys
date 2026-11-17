// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using AdvancedPaste.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public sealed class LocalTransformTests
{
    [TestMethod]
    [DataRow("plain-text", HeadlessTransformFormat.PlainText, "Local text \u4e2d\u6587\n")]
    [DataRow("markdown", HeadlessTransformFormat.Markdown, "<h1>Local</h1><p>Text</p>")]
    [DataRow("json", HeadlessTransformFormat.Json, "<root><value>local</value></root>")]
    public void LocalEntryPointUsesTheExistingTransformation(string name, HeadlessTransformFormat format, string text)
    {
        using var input = new StringReader(text);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = LocalCli.Program.Run(["--format", name], input, output, error, () => true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(HeadlessTransformService.Transform(format, text), output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    [DataRow("paste-with-ai")]
    [DataRow("clipboard")]
    [DataRow("Markdown")]
    [DataRow("")]
    public void UnsupportedFormatsFailWithoutProducingOutput(string name)
    {
        using var input = new StringReader("Must not be transformed");
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.AreEqual(2, LocalCli.Program.Run(["--format", name], input, output, error, () => true));
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "Usage:");
        Assert.AreEqual('M', input.Peek(), "Invalid arguments must be rejected before consuming input.");
    }

    [TestMethod]
    public void DisabledModulePolicyPreventsLocalTransformation()
    {
        using var input = new StringReader("Must not be transformed");
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.AreEqual(1, LocalCli.Program.Run(["--format", "plain-text"], input, output, error, () => false));
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "disabled by Group Policy");
        Assert.AreEqual('M', input.Peek());
    }

    [TestMethod]
    public void OversizedTextFailsWithoutPartialOutput()
    {
        using var input = new StringReader(new string('x', (16 * 1024 * 1024) + 1));
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.AreEqual(1, LocalCli.Program.Run(["--format", "plain-text"], input, output, error, () => true));
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "character limit");
    }
}
