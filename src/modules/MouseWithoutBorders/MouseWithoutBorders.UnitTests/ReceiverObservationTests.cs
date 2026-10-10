// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public class ReceiverObservationTests
{
    [TestMethod]
    public void AcceptsTheForegroundReceiverPanelWithoutCapture()
    {
        Assert.IsTrue(ReceiverObservation.IsPhysicalClickTarget(Observation(), 100));
    }

    [TestMethod]
    [DataRow("Window", 300L)]
    [DataRow("RootWindow", 300L)]
    [DataRow("CaptureWindow", 300L)]
    public void RejectsAnOverlayOrAnotherWindowsCapture(string field, long value)
    {
        var observation = Observation();
        observation["MouseTarget"]![field] = value;
        Assert.IsFalse(ReceiverObservation.IsPhysicalClickTarget(observation, 100));
    }

    [TestMethod]
    [DataRow("ForegroundHwnd", 300L)]
    [DataRow("ReceiverHwnd", 300L)]
    [DataRow("ClickTargetHwnd", 0L)]
    public void RejectsChangedReceiverIdentityOrForeground(string field, long value)
    {
        var observation = Observation();
        observation[field] = value;
        Assert.IsFalse(ReceiverObservation.IsPhysicalClickTarget(observation, 100));
    }

    [TestMethod]
    public void RejectsMissingOrMalformedHitTestEvidence()
    {
        var observation = Observation();
        observation.Remove("MouseTarget");
        Assert.IsFalse(ReceiverObservation.IsPhysicalClickTarget(observation, 100));
        observation["MouseTarget"] = new JsonObject { ["Window"] = "200" };
        Assert.IsFalse(ReceiverObservation.IsPhysicalClickTarget(observation, 100));
        Assert.IsFalse(ReceiverObservation.IsPhysicalClickTarget(Observation(), 0));
    }

    private static JsonObject Observation()
    {
        return JsonNode.Parse("""
            {
              "ReceiverHwnd": 100,
              "ForegroundHwnd": 100,
              "ClickTargetHwnd": 200,
              "MouseTarget": { "Window": 200, "RootWindow": 100, "CaptureWindow": 0 }
            }
            """)!.AsObject();
    }
}
