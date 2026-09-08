// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Output;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests the JSON Lines renderer's stream routing and source-generated serialization for every
/// result envelope accepted by <see cref="ICliOutput"/>.
/// </summary>
[TestClass]
public class JsonCliOutputTests
{
    [TestMethod]
    public void SuccessWriters_EmitOneCompactJsonObjectPerLineToStdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new JsonCliOutput(stdout, stderr);

        output.WriteListResult(new CliListResult
        {
            Monitors = new List<CliMonitorRef>
            {
                new() { Number = 1, Id = "MON-1", Name = "Display", Method = "DDC/CI" },
            },
        });
        output.WriteSetResult(new CliSetResult
        {
            Monitor = new CliMonitorRef { Number = 1, Id = "MON-1", Name = "Display", Method = "DDC/CI" },
            Setting = "brightness",
            BeforeDisplay = "40%",
            AfterDisplay = "50%",
        });
        output.WriteGetResult(new CliGetResult
        {
            Monitors = new List<CliGetMonitorEntry>
            {
                new()
                {
                    Monitor = new CliMonitorRef { Number = 1, Id = "MON-1", Name = "Display", Method = "DDC/CI" },
                    Settings = new List<CliSettingValue>
                    {
                        new() { Setting = "brightness", Display = "50%", Supported = true },
                    },
                },
            },
        });
        output.WriteCapabilitiesResult(new CliCapabilitiesResult
        {
            Monitor = new CliMonitorRef { Number = 1, Id = "MON-1", Name = "Display" },
            CommunicationMethod = "DDC/CI",
            VcpCodes = new List<CliVcpCodeInfo>
            {
                new() { Code = "0x10", Name = "brightness", Continuous = true },
            },
        });
        output.WriteProfileListResult(new CliProfileListResult
        {
            Profiles = new List<CliProfileInfo>
            {
                new() { Id = ProfileTestIds.First, Name = "Work", MonitorCount = 1, LastModified = "2026-08-28T00:00:00Z" },
            },
        });
        output.WriteApplyProfileResult(new CliApplyProfileResult { ProfileId = ProfileTestIds.First, Profile = "Work" });

        Assert.AreEqual(string.Empty, stderr.ToString());
        var lines = Lines(stdout.ToString());
        CollectionAssert.AreEqual(
            new[]
            {
                CliCommandNames.List,
                CliCommandNames.Set,
                CliCommandNames.Get,
                CliCommandNames.Capabilities,
                CliCommandNames.Profiles,
                CliCommandNames.ApplyProfile,
            },
            lines.Select(CommandFromJson).ToArray());

        Assert.AreEqual(6, lines.Count);
        Assert.IsTrue(lines.All(line => line.StartsWith('{') && line.EndsWith('}')));
        Assert.IsTrue(lines.All(line => !line.Contains('\r') && !line.Contains('\n')));
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.AreEqual("2.0", document.RootElement.GetProperty("version").GetString());
        }

        using var profilesDocument = JsonDocument.Parse(lines[4]);
        var listedId = profilesDocument.RootElement.GetProperty("profiles")[0].GetProperty("id");
        Assert.AreEqual(JsonValueKind.String, listedId.ValueKind);
        Assert.AreEqual(ProfileTestIds.FirstText, listedId.GetString());
        using var appliedDocument = JsonDocument.Parse(lines[5]);
        var appliedId = appliedDocument.RootElement.GetProperty("profileId");
        Assert.AreEqual(JsonValueKind.String, appliedId.ValueKind);
        Assert.AreEqual(ProfileTestIds.FirstText, appliedId.GetString());
    }

    [TestMethod]
    public void WriteProfileListResult_SpecialNameRoundTripsOnOnePhysicalLine()
    {
        const string ProfileName = "Office | Focus \"quoted\"\n\u7b2c\u4e8c\u884c";
        var stdout = new StringWriter();
        var output = new JsonCliOutput(stdout, new StringWriter());

        output.WriteProfileListResult(new CliProfileListResult
        {
            Profiles = new List<CliProfileInfo>
            {
                new() { Id = ProfileTestIds.Second, Name = ProfileName, MonitorCount = 2 },
            },
        });

        var lines = Lines(stdout.ToString());
        Assert.AreEqual(1, lines.Count, "an embedded newline must be JSON-escaped");
        var roundTripped = JsonSerializer.Deserialize(
            lines[0],
            ContractsJsonContext.Default.CliProfileListResult);
        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(ProfileName, roundTripped.Profiles.Single().Name);
    }

    [TestMethod]
    public void WriteError_EmitsJsonOnlyToStderr()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new JsonCliOutput(stdout, stderr);

        output.WriteError(new CliErrorResult
        {
            Command = CliCommandNames.ApplyProfile,
            Error = new CliError
            {
                Code = CliErrorCodes.ArgumentError,
                Message = "Profile not found.",
                Hint = "Run profiles first.",
            },
        });

        Assert.AreEqual(string.Empty, stdout.ToString());
        var lines = Lines(stderr.ToString());
        Assert.AreEqual(1, lines.Count);
        var error = JsonSerializer.Deserialize(lines[0], ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error);
        Assert.IsTrue(error.IsError);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual("Run profiles first.", error.Error.Hint);
    }

    [TestMethod]
    public void WriteError_ProfileNotFoundMessageId_LocalizesCopyAndPreservesStructuredFields()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new JsonCliOutput(stdout, stderr);
        var source = new CliErrorResult
        {
            Command = CliCommandNames.ApplyProfile,
            Error = new CliError
            {
                Code = CliErrorCodes.ArgumentError,
                MessageId = CliMessageIds.ProfileNotFound,
                Value = ProfileTestIds.UnknownText,
                Detail = "profile store lookup completed",
            },
        };

        output.WriteError(source);

        Assert.AreEqual(string.Empty, stdout.ToString());
        Assert.AreEqual(string.Empty, source.Error.Message, "the source envelope must not be mutated");
        Assert.IsNull(source.Error.Hint, "the source envelope must not be mutated");

        var lines = Lines(stderr.ToString());
        Assert.AreEqual(1, lines.Count);
        var error = JsonSerializer.Deserialize(lines[0], ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual(CliMessageIds.ProfileNotFound, error.Error.MessageId);
        Assert.AreEqual(ProfileTestIds.UnknownText, error.Error.Value);
        Assert.AreEqual("profile store lookup completed", error.Error.Detail);
        Assert.AreEqual($"no profile with id {ProfileTestIds.UnknownText}", error.Error.Message);
        Assert.AreEqual("run 'PowerToys.PowerDisplay.Cli.exe profiles' to see available profiles", error.Error.Hint);
    }

    [TestMethod]
    public void WriteWarning_IsSilent()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new JsonCliOutput(stdout, stderr);

        output.WriteWarning("monitor number ignored");

        Assert.AreEqual(string.Empty, stdout.ToString());
        Assert.AreEqual(string.Empty, stderr.ToString());
    }

    private static List<string> Lines(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToList();

    private static string CommandFromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("command").GetString() ?? string.Empty;
    }
}
