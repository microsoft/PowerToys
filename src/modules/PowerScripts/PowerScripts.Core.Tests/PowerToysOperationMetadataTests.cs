// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class PowerToysOperationMetadataTests
{
    [TestMethod]
    public void NativeOperationIsDisplayedWithoutInventingAPowerShellRuntimeOrCustomCommand()
    {
        var descriptor = JsonNode.Parse("""
            {
              "title": "List layouts",
              "inputSchema": {"type":"object", "properties":{}},
              "outputSchema": {"type":"object", "properties":{"text":{"type":"string"}}},
              "x-execute": {"provider":"powertoys-cli"}
            }
            """)!.AsObject();
        var original = descriptor.ToJsonString();

        var rows = PowerScriptMetadataDetails.Create(descriptor, key => key, ".json")
            .SelectMany(section => section.Rows).ToDictionary(row => row.Key);

        Assert.AreEqual("PowerScripts_DetailsPowerToysOperation", rows["Runtime"].Value);
        Assert.AreEqual("PowerScripts_DetailsPowerToysOperationMode", rows["ExecutionMode"].Value);
        Assert.IsFalse(rows.ContainsKey("Command"));
        Assert.IsFalse(rows.ContainsKey("StandardInput"));
        Assert.AreEqual(original, descriptor.ToJsonString());
    }

    [TestMethod]
    public void OrdinaryExplicitRecipeRetainsItsRuntimeAndArgumentPresentation()
    {
        var descriptor = JsonNode.Parse("""
            {
              "inputSchema": {"type":"object", "properties":{}},
              "outputSchema": {"type":"object", "properties":{}},
              "x-execute": {"command":["python", "script with spaces.py"], "stdin":"json"}
            }
            """)!.AsObject();

        var rows = PowerScriptMetadataDetails.Create(descriptor, key => key, ".py")
            .SelectMany(section => section.Rows).ToDictionary(row => row.Key);

        Assert.AreEqual("Python", rows["Runtime"].Value);
        Assert.AreEqual("PowerScripts_DetailsCustom", rows["ExecutionMode"].Value);
        Assert.AreEqual("PowerScripts_DetailsJsonInput", rows["StandardInput"].Value);
        StringAssert.Contains(rows["Command"].Value, "script with spaces.py");
    }
}
