// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class RecoveryOutcomeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RecoveryRequiresTheExactExpectedVerdict(bool clipboardLost)
    {
        var runId = Guid.NewGuid().ToString();
        RecoveryOutcome.Require(Result(runId, clipboardLost), runId, clipboardLost);
    }

    [TestMethod]
    [DataRow("RunId")]
    [DataRow("Status")]
    [DataRow("SettingsRestored")]
    [DataRow("ClipboardRestored")]
    [DataRow("RequiresBaselineReset")]
    [DataRow("FormatVersion")]
    public void MissingOwnershipOrVerdictFieldsCannotPass(string field)
    {
        var runId = Guid.NewGuid().ToString();
        var result = Result(runId, clipboardLost: false);
        result.Remove(field);
        Assert.ThrowsExactly<InvalidDataException>(() => RecoveryOutcome.Require(result, runId, clipboardLost: false));
    }

    [TestMethod]
    public void SuccessfulCleanupCannotHideAnUnexpectedFault()
    {
        var runId = Guid.NewGuid().ToString();
        var result = Result(runId, clipboardLost: false);
        result["Errors"]!.AsArray().Add("An unrelated resource was not stopped.");
        Assert.ThrowsExactly<InvalidDataException>(() => RecoveryOutcome.Require(result, runId, clipboardLost: false));
    }

    [TestMethod]
    public void BaselineResetCannotHideAnUnrelatedRecoveryFailure()
    {
        var runId = Guid.NewGuid().ToString();
        var result = Result(runId, clipboardLost: true);
        result["Errors"]!.AsArray()[0] = "An unowned Sandbox is still active.";
        Assert.ThrowsExactly<InvalidDataException>(() => RecoveryOutcome.Require(result, runId, clipboardLost: true));
        result["Errors"] = new JsonArray(RecoveryOutcome.LostClipboardError);
        result["RequiresBaselineReset"] = false;
        Assert.ThrowsExactly<InvalidDataException>(() => RecoveryOutcome.Require(result, runId, clipboardLost: true));
    }

    [TestMethod]
    public void AnotherRunCannotSatisfyRecovery()
    {
        var runId = Guid.NewGuid().ToString();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            RecoveryOutcome.Require(Result(Guid.NewGuid().ToString(), clipboardLost: false), runId, clipboardLost: false));
    }

    [TestMethod]
    public void ProcessBirthTimesSupportBothHarnessSerializersWithoutGuessing()
    {
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(1791242662631).UtcDateTime;
        Assert.AreEqual(expected, RecoveryOutcome.ProcessTimestamp(JsonValue.Create("2026-10-05T23:24:22.631Z")));
        Assert.AreEqual(expected, RecoveryOutcome.ProcessTimestamp(JsonValue.Create("/Date(1791242662631)/")));
    }

    [TestMethod]
    [DataRow("2026-10-05T23:24:22.631")]
    [DataRow("/Date(invalid)/")]
    [DataRow("unknown")]
    public void ProcessBirthTimeNeverAssumesAnUnspecifiedClock(string value)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => RecoveryOutcome.ProcessTimestamp(JsonValue.Create(value)));
    }

    private static JsonObject Result(string runId, bool clipboardLost) => new()
    {
        ["FormatVersion"] = 1, ["RunId"] = runId, ["Status"] = clipboardLost ? "Incomplete" : "Recovered",
        ["SettingsRestored"] = true, ["ClipboardRestored"] = !clipboardLost, ["RequiresBaselineReset"] = clipboardLost,
        ["Errors"] = clipboardLost ? new JsonArray(RecoveryOutcome.LostClipboardError) : new JsonArray(),
    };
}
