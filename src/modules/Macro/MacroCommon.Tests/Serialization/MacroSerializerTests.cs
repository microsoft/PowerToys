// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroCommon.Tests.Serialization;

[TestClass]
public sealed class MacroSerializerTests
{
    [TestMethod]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new MacroDefinition
        {
            Id = "test-id",
            Name = "Test Macro",
            Description = "A test",
            Hotkey = "Ctrl+Shift+T",
            AppScope = "notepad.exe",
            Steps =
            [
                new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" },
                new MacroStep { Type = StepType.Wait, Ms = 200 },
                new MacroStep { Type = StepType.TypeText, Text = "Hello" },
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };

        var json = MacroSerializer.Serialize(original);
        var restored = MacroSerializer.Deserialize(json);

        Assert.AreEqual(original.Id, restored.Id);
        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Hotkey, restored.Hotkey);
        Assert.AreEqual(original.AppScope, restored.AppScope);
        Assert.AreEqual(4, restored.Steps.Count);
        Assert.AreEqual(StepType.PressKey, restored.Steps[0].Type);
        Assert.AreEqual("Ctrl+C", restored.Steps[0].Key);
        Assert.AreEqual(200, restored.Steps[1].Ms);
        Assert.AreEqual("Hello", restored.Steps[2].Text);
        Assert.AreEqual(3, restored.Steps[3].Count);
        Assert.AreEqual(1, restored.Steps[3].Steps?.Count);
        Assert.AreEqual(original.Description, restored.Description);
        Assert.AreEqual("Tab", restored.Steps[3].Steps![0].Key);
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseKeys()
    {
        var macro = new MacroDefinition { Name = "Test", AppScope = "notepad.exe" };
        var json = MacroSerializer.Serialize(macro);
        StringAssert.Contains(json, "\"app_scope\"");
        Assert.IsFalse(json.Contains("\"appScope\""), "camelCase key must not appear");
    }

    [TestMethod]
    public void Serialize_OmitsNullOptionalFields()
    {
        var macro = new MacroDefinition { Name = "Test" };
        var json = MacroSerializer.Serialize(macro);
        Assert.IsFalse(json.Contains("\"app_scope\""), "null app_scope should be omitted");
        Assert.IsFalse(json.Contains("\"hotkey\""), "null hotkey should be omitted");
    }

    [TestMethod]
    public void Deserialize_StepTypeUsesSnakeCase()
    {
        var json = """
            {
              "id": "x",
              "name": "T",
              "steps": [{ "type": "press_key", "key": "Enter" }]
            }
            """;
        var macro = MacroSerializer.Deserialize(json);
        Assert.AreEqual(StepType.PressKey, macro.Steps[0].Type);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Deserialize_NullJson_Throws()
    {
        MacroSerializer.Deserialize("null");
    }

    [TestMethod]
    public void Deserialize_MalformedJson_ThrowsJsonException()
    {
        Assert.ThrowsException<System.Text.Json.JsonException>(
            () => MacroSerializer.Deserialize("{ not valid"));
    }
}
