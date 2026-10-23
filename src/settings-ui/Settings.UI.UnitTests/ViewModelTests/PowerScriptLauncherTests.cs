// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace ViewModelTests;

[TestClass]
public class PowerScriptLauncherTests
{
    private static readonly string[] ExpectedActionIds = { "alpha-id", "z" };

    [TestMethod]
    public void DiscoveryIncludesOnlyNoInputScriptsAndSearchesPresentationAndIdentity()
    {
        var scripts = new[]
        {
            new PowerScriptInfo { Id = "other", Name = "File action", Input = PowerScriptIO.Files },
            new PowerScriptInfo { Id = "text", Name = "Text action", Input = PowerScriptIO.Text },
            new PowerScriptInfo { Id = "z", Name = "Zulu", Description = "Create a report" },
            new PowerScriptInfo { Id = "alpha-id", Name = "Alpha" },
        };
        CollectionAssert.AreEqual(ExpectedActionIds, PowerScriptLauncherModel.FilterScripts(scripts, string.Empty).Select(script => script.Id).ToArray());
        Assert.AreEqual("z", PowerScriptLauncherModel.FilterScripts(scripts, "REPORT").Single().Id);
        Assert.AreEqual("alpha-id", PowerScriptLauncherModel.FilterScripts(scripts, "ALPHA-ID").Single().Id);
        Assert.AreEqual(0, PowerScriptLauncherModel.FilterScripts(scripts, "File action").Count);
    }

    [TestMethod]
    public void ParametersPreserveFalseZeroAndExplicitEmptyString()
    {
        var declarations = new[]
        {
            Parameter("enabled", "bool"),
            Parameter("count", "int"),
            Parameter("label", "string", recommended: "recommended"),
            Parameter("optional", "choice"),
        };
        var values = new Dictionary<string, string>
        {
            ["enabled"] = "false",
            ["count"] = "0",
            ["label"] = string.Empty,
            ["optional"] = null,
        };
        Assert.IsTrue(PowerScriptLauncherModel.TryCollectParameters(declarations, values, out var result, out _));
        Assert.AreEqual("false", result["enabled"]);
        Assert.AreEqual("0", result["count"]);
        Assert.AreEqual(string.Empty, result["label"]);
        Assert.IsFalse(result.ContainsKey("optional"), "Unset optional typed fields must not be sent as invalid empty values.");
    }

    [TestMethod]
    public void MissingRequiredParameterBlocksExecutionWithoutPartialParameters()
    {
        var declarations = new[] { Parameter("first", "string"), Parameter("required", "string", required: true) };
        var values = new Dictionary<string, string> { ["first"] = "value", ["required"] = string.Empty };
        Assert.IsFalse(PowerScriptLauncherModel.TryCollectParameters(declarations, values, out var result, out var missing));
        Assert.AreEqual("required", missing);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void UnsetParameterCanInheritARequiredDefaultFromTheHost()
    {
        var declarations = new[] { Parameter("choice", "choice", required: true, recommended: "default") };
        Assert.IsTrue(PowerScriptLauncherModel.TryCollectParameters(declarations, new Dictionary<string, string>(), out var result, out _));
        Assert.AreEqual(0, result.Count);
    }

    private static PowerScriptParameter Parameter(string name, string type, bool required = false, string recommended = null) =>
        new(name, type, required, null, null, recommended, System.Array.Empty<string>(), null, null);
}
