// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class PowerScriptTemplateParameterTests
{
    [TestMethod]
    public void ParameterEditorPreservesFalseZeroEmptyAndUnsetDefaults()
    {
        var parameter = new PowerScriptTemplateParameter(1) { Name = "mode", TypeIndex = 0 };
        Assert.IsFalse(parameter.ToTemplateParameter()["schema"].AsObject().ContainsKey("default"));
        parameter.HasDefault = true;
        Assert.AreEqual(string.Empty, parameter.ToTemplateParameter()["schema"]["default"].GetValue<string>());
        parameter.TypeIndex = 1;
        Assert.IsFalse(parameter.HasDefault, "Changing type must not silently reinterpret an old default.");
        parameter.HasDefault = true;
        parameter.DefaultText = "0";
        Assert.AreEqual(0, parameter.ToTemplateParameter()["schema"]["default"].GetValue<int>());
        parameter.TypeIndex = 2;
        parameter.HasDefault = true;
        Assert.IsFalse(parameter.ToTemplateParameter()["schema"]["default"].GetValue<bool>());
    }

    [TestMethod]
    [DataRow("short\r\nfull")]
    [DataRow("short\nfull")]
    [DataRow("short\rfull")]
    public void ParameterEditorOnlySerializesConstraintsForTheSelectedType(string choices)
    {
        var parameter = new PowerScriptTemplateParameter(1)
        {
            Name = "mode",
            TypeIndex = 0,
            EnumText = choices,
            Required = true,
            MinimumText = "2",
            MaximumText = "5",
        };
        var definition = parameter.ToTemplateParameter();
        Assert.IsTrue(definition["required"].GetValue<bool>());
        Assert.AreEqual("full", definition["schema"]["enum"][1].GetValue<string>());
        Assert.IsNull(definition["schema"]["minimum"]);
        parameter.TypeIndex = 1;
        definition = parameter.ToTemplateParameter();
        Assert.AreEqual(2, definition["schema"]["minimum"].GetValue<int>());
        Assert.AreEqual(5, definition["schema"]["maximum"].GetValue<int>());
        Assert.IsNull(definition["schema"]["enum"]);
        parameter.TypeIndex = 3;
        parameter.FileExtensions = ".txt, .json";
        definition = parameter.ToTemplateParameter();
        Assert.AreEqual("file-path", definition["schema"]["format"].GetValue<string>());
        Assert.AreEqual(".txt, .json", definition["fileExtensions"].GetValue<string>());
        Assert.IsNull(definition["schema"]["minimum"]);
    }

    [TestMethod]
    public void ParameterTypeMustBeChosenBeforeCreatingMetadata()
    {
        var parameter = new PowerScriptTemplateParameter(1) { Name = "mode" };
        Assert.ThrowsException<InvalidOperationException>(() => parameter.ToTemplateParameter());
        parameter.TypeIndex = 0;
        Assert.AreEqual("string", parameter.ToTemplateParameter()["schema"]["type"].GetValue<string>());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void RequiredRuntimeInputsDoNotInventDefaults(int typeIndex)
    {
        var parameter = new PowerScriptTemplateParameter(1) { Name = "config", TypeIndex = typeIndex, Required = true };
        var definition = parameter.ToTemplateParameter();
        Assert.IsTrue(definition["required"].GetValue<bool>());
        Assert.IsFalse(definition["schema"].AsObject().ContainsKey("default"));
    }
}
