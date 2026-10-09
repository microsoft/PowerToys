// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LightSwitch.Cli.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.Cli.UnitTests;

[TestClass]
public sealed class CliProtocolTests
{
    private static readonly byte[] ExpectedEncodedLine = { 0x41, 0x00, 0x0A, 0x00 };

    [TestMethod]
    [DataRow("null")]
    [DataRow("not-json")]
    [DataRow("{}")]
    [DataRow("{\"version\":1,\"state\":{}}")]
    [DataRow("{\"version\":1,\"success\":true}")]
    [DataRow("{\"version\":1,\"success\":false}")]
    [DataRow("{\"version\":2,\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":\"Failed.\"}}")]
    [DataRow("{\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":\"Failed.\"}}")]
    [DataRow("{\"version\":1,\"success\":false,\"error\":{\"code\":\"\",\"message\":\"Failed.\"}}")]
    [DataRow("{\"version\":1,\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":null}}")]
    [DataRow("{\"version\":1,\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\"}}")]
    [DataRow("{\"version\":1,\"success\":false,\"error\":{\"message\":\"Failed.\"}}")]
    public void InvalidEnvelopesAreProtocolErrors(string json)
    {
        var error = Assert.ThrowsException<CliException>(() => CliProtocol.ParseResponse(json));
        Assert.AreEqual("PROTOCOL_ERROR", error.Code);
    }

    [TestMethod]
    [DataRow("systemTheme", "null")]
    [DataRow("systemTheme", "\"sepia\"")]
    [DataRow("appsTheme", "\"Dark\"")]
    [DataRow("scheduleMode", "\"automatic\"")]
    [DataRow("changeSystem", "\"true\"")]
    public void InvalidStateMembersAreRejected(string property, string replacement)
    {
        string original = property switch
        {
            "systemTheme" => "\"light\"",
            "appsTheme" => "\"dark\"",
            "scheduleMode" => "\"FixedHours\"",
            _ => "true",
        };
        string json = CliApplicationTests.SuccessfulResponse.Replace("\"" + property + "\":" + original, "\"" + property + "\":" + replacement, StringComparison.Ordinal);

        var error = Assert.ThrowsException<CliException>(() => CliProtocol.ParseResponse(json));
        Assert.AreEqual("PROTOCOL_ERROR", error.Code);
    }

    [TestMethod]
    public void MissingFalseValuedStateMembersAreNotSilentlyDefaulted()
    {
        string json = CliApplicationTests.SuccessfulResponse.Replace("\"changeApps\":false,", string.Empty, StringComparison.Ordinal);
        Assert.ThrowsException<CliException>(() => CliProtocol.ParseResponse(json));
    }

    [TestMethod]
    public void ContradictorySuccessAndErrorAreRejected()
    {
        string json = CliApplicationTests.SuccessfulResponse.Replace("\"success\":true,", "\"success\":true,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":\"Failed.\"},", StringComparison.Ordinal);
        Assert.ThrowsException<CliException>(() => CliProtocol.ParseResponse(json));
    }

    [TestMethod]
    public void UnknownThemeIsAValidState()
    {
        string json = CliApplicationTests.SuccessfulResponse.Replace("\"systemTheme\":\"light\"", "\"systemTheme\":\"unknown\"", StringComparison.Ordinal);
        Assert.AreEqual("unknown", CliProtocol.ParseResponse(json).State!.SystemTheme);
    }

    [TestMethod]
    public void ErrorCanIncludeTheResultingState()
    {
        string json = CliApplicationTests.SuccessfulResponse.Replace("\"success\":true,", "\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":\"Partially applied.\"},", StringComparison.Ordinal);
        var response = CliProtocol.ParseResponse(json);
        Assert.IsFalse(response.Success);
        Assert.IsNotNull(response.State);
        Assert.AreEqual("EXECUTION_FAILED", response.Error!.Code);
    }

    [TestMethod]
    public void OversizedEnvelopeIsRejectedBeforeDeserialization()
    {
        Assert.ThrowsException<CliException>(() => CliProtocol.ParseResponse(new string(' ', CliProtocol.MaxMessageChars + 1)));
    }

    [TestMethod]
    public void PipeEncodingHasNoBomAndUsesLittleEndianUtf16()
    {
        Assert.AreEqual(0, CliProtocol.Encoding.GetPreamble().Length);
        CollectionAssert.AreEqual(ExpectedEncodedLine, CliProtocol.Encoding.GetBytes("A\n"));
    }
}
