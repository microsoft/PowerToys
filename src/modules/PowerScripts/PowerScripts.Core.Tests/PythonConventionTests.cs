// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Tests;

[TestClass]
public class PythonConventionTests
{
    [TestMethod]
    public void Parse_TextToText_ReturnsSignature()
    {
        var signature = PowerScriptPythonConvention.Parse("def powerscript_from_text_to_text(text):\n    return text.upper()\n");

        Assert.IsNotNull(signature);
        Assert.AreEqual("powerscript_from_text_to_text", signature.FunctionName);
        Assert.AreEqual(PowerScriptDataFormat.Text, signature.Input);
        Assert.AreEqual(PowerScriptDataFormat.Text, signature.Output);
    }

    [TestMethod]
    public void Parse_ImageToText_MapsFormats()
    {
        var signature = PowerScriptPythonConvention.Parse("def powerscript_from_image_to_text(image_path):\n    return ''\n");

        Assert.IsNotNull(signature);
        Assert.AreEqual(PowerScriptDataFormat.Image, signature.Input);
        Assert.AreEqual(PowerScriptDataFormat.Text, signature.Output);
    }

    [TestMethod]
    public void Parse_FileOutputToken_MapsToFiles()
    {
        var signature = PowerScriptPythonConvention.Parse("def powerscript_from_text_to_file(text):\n    return []\n");

        Assert.IsNotNull(signature);
        Assert.AreEqual(PowerScriptDataFormat.Text, signature.Input);
        Assert.AreEqual(PowerScriptDataFormat.Files, signature.Output);
    }

    [TestMethod]
    public void Parse_NoMatchingFunction_ReturnsNull()
    {
        Assert.IsNull(PowerScriptPythonConvention.Parse("def do_something(x):\n    return x\n"));
    }

    [TestMethod]
    public void Parse_MultipleMatchingFunctions_ReturnsNull()
    {
        var source =
            "def powerscript_from_text_to_text(text):\n    return text\n\n" +
            "def powerscript_from_html_to_text(html):\n    return html\n";

        Assert.IsNull(PowerScriptPythonConvention.Parse(source));
    }

    [TestMethod]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(PowerScriptPythonConvention.Parse(null!));
        Assert.IsNull(PowerScriptPythonConvention.Parse(string.Empty));
    }
}
