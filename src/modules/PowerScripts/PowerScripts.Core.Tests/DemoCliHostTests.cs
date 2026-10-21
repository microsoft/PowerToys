// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

/// <summary>Exercises only the Host's fixed diagnostic echo command, without configuration or native CLI execution.</summary>
[TestClass]
public class DemoCliHostTests
{
    private static string _hostPath = string.Empty;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _hostPath = PowerScriptsClient.ResolveHostPath() ?? string.Empty;
        Assert.IsFalse(string.IsNullOrEmpty(_hostPath), "Build the referenced PowerScripts.Host project before running demo CLI contract tests.");
    }

    [TestMethod]
    public void Echo_UsesDefaultsAndPreservesOriginalArgumentTokens()
    {
        var response = RunEcho("echo", "--target", "demo target");
        var parameters = response.GetProperty("parameters");

        CollectionAssert.AreEqual(new[] { "--target", "demo target" }, ReadArguments(response));
        Assert.AreEqual("demo target", parameters.GetProperty("target").GetString());
        Assert.AreEqual("fast", parameters.GetProperty("mode").GetString());
        Assert.AreEqual(1, parameters.GetProperty("count").GetInt32());
        Assert.IsFalse(parameters.GetProperty("verbose").GetBoolean());
    }

    [TestMethod]
    public void Echo_MapsTypedParametersAndPreservesQuotesUnicodePercentAndNewlines()
    {
        const string target = "你好 \"quoted\" %PATH%\r\n$(noop) & | >";
        string[] arguments = ["echo", "--count", "100", "--verbose", "--target", target, "--mode", "full"];
        var response = RunEcho(arguments);
        var parameters = response.GetProperty("parameters");

        CollectionAssert.AreEqual(arguments.Skip(1).ToArray(), ReadArguments(response));
        Assert.AreEqual(target, parameters.GetProperty("target").GetString());
        Assert.AreEqual("full", parameters.GetProperty("mode").GetString());
        Assert.AreEqual(100, parameters.GetProperty("count").GetInt32());
        Assert.IsTrue(parameters.GetProperty("verbose").GetBoolean());
    }

    [DataTestMethod]
    [DataRow("--mode")]
    [DataRow("--verbose")]
    [DataRow("--")]
    public void Echo_TargetBeginningWithDashesRemainsOneValue(string target)
    {
        var response = RunEcho("echo", "--target", target);

        Assert.AreEqual(target, response.GetProperty("parameters").GetProperty("target").GetString());
        Assert.IsFalse(response.GetProperty("parameters").GetProperty("verbose").GetBoolean());
        CollectionAssert.AreEqual(new[] { "--target", target }, ReadArguments(response));
    }

    [DataTestMethod]
    [DataRow("1", 1)]
    [DataRow("100", 100)]
    public void Echo_AcceptsBothCountBoundaries(string value, int expected)
    {
        var response = RunEcho("echo", "--target", "demo", "--count", value);

        Assert.AreEqual(expected, response.GetProperty("parameters").GetProperty("count").GetInt32());
    }

    [DataTestMethod]
    [DataRow("--unknown")]
    [DataRow("--json")]
    [DataRow("--root")]
    public void Echo_RejectsUnknownOptions(string option)
    {
        AssertFailure("echo", "--target", "demo", option);
    }

    [DataTestMethod]
    [DataRow("--target", "second")]
    [DataRow("--mode", "full")]
    [DataRow("--count", "2")]
    public void Echo_RejectsDuplicateValuedOptions(string option, string value)
    {
        AssertFailure("echo", "--target", "demo", "--mode", "fast", "--count", "1", option, value);
    }

    [TestMethod]
    public void Echo_RejectsDuplicateSwitches()
    {
        AssertFailure("echo", "--target", "demo", "--verbose", "--verbose");
    }

    [DataTestMethod]
    [DataRow("--target")]
    [DataRow("--mode")]
    [DataRow("--count")]
    public void Echo_RejectsOptionsWithoutValues(string option)
    {
        AssertFailure("echo", option);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("101")]
    [DataRow("-1")]
    [DataRow("1.5")]
    [DataRow("invalid")]
    [DataRow(" 1")]
    [DataRow("+1")]
    public void Echo_RejectsInvalidCount(string count)
    {
        AssertFailure("echo", "--target", "demo", "--count", count);
    }

    [DataTestMethod]
    [DataRow("standard")]
    [DataRow("quiet")]
    [DataRow("FAST")]
    [DataRow("")]
    public void Echo_RejectsInvalidMode(string mode)
    {
        AssertFailure("echo", "--target", "demo", "--mode", mode);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Echo_RejectsEmptyTarget(string target)
    {
        AssertFailure("echo", "--target", target);
    }

    [TestMethod]
    public void Echo_RequiresTarget()
    {
        AssertFailure("echo");
    }

    [TestMethod]
    public void Echo_RejectsAdditionalPositionals()
    {
        AssertFailure("echo", "--target", "demo", "unexpected");
    }

    [TestMethod]
    public void Demo_RejectsUnknownOrMissingCommand()
    {
        AssertFailure("unknown", "--target", "demo");
        AssertFailure();
    }

    private static string[] ReadArguments(JsonElement response) =>
        response.GetProperty("arguments").EnumerateArray().Select(argument => argument.GetString()!).ToArray();

    private static JsonElement RunEcho(params string[] arguments)
    {
        var (exitCode, stdout, stderr) = RunHost(arguments);
        Assert.AreEqual(0, exitCode, stdout + stderr);
        Assert.AreEqual(string.Empty, stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.AreEqual(2, document.RootElement.EnumerateObject().Count());
        Assert.IsFalse(document.RootElement.TryGetProperty("error", out _));
        return document.RootElement.Clone();
    }

    private static void AssertFailure(params string[] arguments)
    {
        var (exitCode, stdout, stderr) = RunHost(arguments);
        Assert.AreEqual(1, exitCode, stderr);
        Assert.AreEqual(string.Empty, stderr);
        using var document = JsonDocument.Parse(stdout);
        var response = document.RootElement;
        Assert.AreEqual(JsonValueKind.Object, response.ValueKind);
        Assert.AreEqual(1, response.EnumerateObject().Count());
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.GetProperty("error").GetString()));
    }

    private static (int ExitCode, string StdOut, string StdErr) RunHost(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(_hostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("demo");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
            Assert.Fail("The fixed Host demo command did not complete within 15 seconds.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
