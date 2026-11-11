// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class KbmPowerScriptInvocationTests
{
    [TestMethod]
    public void ScriptInvocationPreservesConsumerAndLiteralValues()
    {
        var values = new Dictionary<string, string?>
        {
            ["path"] = "C:\\Documents\\a \"quoted\" 100% 中文.txt",
            ["empty"] = string.Empty,
            ["flag"] = "false",
            ["count"] = "0",
            ["lines"] = "first\r\nsecond",
            ["omitted"] = null,
        };
        string arguments = PowerScriptInvocation.BuildArguments("file-query-8f72", values);

        StringAssert.Contains(arguments, "--consumer keyboard-manager");
        Assert.IsTrue(PowerScriptInvocation.TryParseArguments(arguments, out string scriptId, out var restored));
        Assert.AreEqual("file-query-8f72", scriptId);
        Assert.AreEqual(5, restored.Count);
        foreach (var pair in values.Where(pair => pair.Value is not null))
        {
            Assert.AreEqual(pair.Value, restored[pair.Key]);
        }
    }

    [TestMethod]
    public void ExistingConsumerBindingAndOlderSilentBindingRemainRecognizable()
    {
        Assert.IsTrue(PowerScriptInvocation.TryParseArguments("run layouts --consumer keyboard-manager --no-consent", out _, out _));
        Assert.IsTrue(PowerScriptInvocation.TryParseArguments("run layouts --no-consent", out _, out _));
    }

    [DataTestMethod]
    [DataRow("run layouts --consumer command-palette --no-consent")]
    [DataRow("run layouts --consumer keyboard-manager --consumer keyboard-manager --no-consent")]
    [DataRow("run layouts --no-consent --root C:\\OtherScripts")]
    [DataRow("run layouts --no-consent --files C:\\input.txt")]
    [DataRow("run layouts --consumer keyboard-manager")]
    [DataRow("run layouts --no-consent --no-consent")]
    [DataRow("run layouts --no-consent --set-base64")]
    [DataRow("run layouts --no-consent --set-base64 invalid!")]
    public void UnrecognizedInvocationsRemainOrdinaryProgramMappings(string arguments)
    {
        Assert.IsFalse(PowerScriptInvocation.TryParseArguments(arguments, out _, out _));
    }

    [TestMethod]
    public void DuplicateParameterAndInvalidUtf8AreRejected()
    {
        string parameter = Convert.ToBase64String(Encoding.UTF8.GetBytes("path\0C:\\a.txt"));
        Assert.IsFalse(PowerScriptInvocation.TryParseArguments("run query --no-consent --set-base64 " + parameter + " " + parameter, out _, out _));
        Assert.IsFalse(PowerScriptInvocation.TryParseArguments("run query --no-consent --set-base64 /w==", out _, out _));
    }

    [TestMethod]
    public void CommandLineLengthIncludesQuotedHostAndTerminator()
    {
        const string host = "C:\\PowerToys\\PowerScripts\\PowerScripts.Host.exe";
        int remaining = 32767 - host.Length - 4;
        Assert.IsTrue(PowerScriptInvocation.TryValidateLength(host, new string('a', remaining), out _));
        Assert.IsFalse(PowerScriptInvocation.TryValidateLength(host, new string('a', remaining + 1), out _));
    }

    [TestMethod]
    public void NullValuesAreOmittedAndNullCharactersAreRejected()
    {
        string arguments = PowerScriptInvocation.BuildArguments("query", new Dictionary<string, string?> { ["path"] = null });
        Assert.IsFalse(arguments.Contains("--set-base64", StringComparison.Ordinal));
        Assert.ThrowsException<ArgumentException>(() => PowerScriptInvocation.BuildArguments("query", new Dictionary<string, string?> { ["path"] = "a\0b" }));
    }
}
