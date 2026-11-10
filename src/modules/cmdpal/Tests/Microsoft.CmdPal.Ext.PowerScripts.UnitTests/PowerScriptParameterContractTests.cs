// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptParameterContractTests
{
    [TestMethod]
    public void LabelsHelpAndSchemaAnnotationsDoNotInvalidateEnteredValues()
    {
        var previous = new PowerScriptInfo
        {
            Parameters = [new PowerScriptParameter("value", "string", true, "Old label", "Old help", null, [], null, null)],
            InputSchema = TestScripts.Json("""
                {"title":"Old title","description":"Old schema help","type":"object","properties":{"parameters":{"type":"object","properties":{"value":{"type":"string","title":"Old field title","description":"Old field help","minLength":1,"maxLength":10}}}}}
                """),
        };
        var current = new PowerScriptInfo
        {
            Parameters = [new PowerScriptParameter("value", "string", true, "New label", "New help", null, [], null, null)],
            InputSchema = TestScripts.Json("""
                {"properties":{"parameters":{"properties":{"value":{"maxLength":10,"description":"New field help","minLength":1,"title":"New field title","type":"string"}},"type":"object"}},"type":"object","description":"New schema help","title":"New title"}
                """),
        };

        Assert.IsTrue(PowerScriptsService.HasSameParameterContract(previous, current));
    }

    [TestMethod]
    [DataRow("{\"type\":\"string\",\"minLength\":1}", "{\"type\":\"string\",\"minLength\":2}")]
    [DataRow("{\"type\":\"string\",\"maxLength\":10}", "{\"type\":\"string\",\"maxLength\":9}")]
    [DataRow("{\"type\":\"string\",\"pattern\":\"^a+$\"}", "{\"type\":\"string\",\"pattern\":\"^b+$\"}")]
    [DataRow("{\"type\":\"string\",\"default\":\"one\"}", "{\"type\":\"string\",\"default\":\"two\"}")]
    [DataRow("{\"type\":\"integer\",\"minimum\":0}", "{\"type\":\"integer\",\"minimum\":1}")]
    [DataRow("{\"type\":\"integer\",\"maximum\":10}", "{\"type\":\"integer\",\"maximum\":11}")]
    public void AuthoritativeSchemaConstraintChangesInvalidateEnteredValues(string previousProperty, string currentProperty)
    {
        var previous = SchemaScript("value", previousProperty);
        var current = SchemaScript("value", currentProperty);

        Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, current));
    }

    [TestMethod]
    public void TypedDefaultsBoundsNamesAndRequiredFlagsArePartOfTheParameterContract()
    {
        var parameter = TestScripts.Parameter("count", "int", required: true, defaultValue: "0", min: 0, max: 10);
        var previous = TestScripts.Create(parameter);
        PowerScriptParameter[] changedParameters =
        [
            parameter with { Default = "1" },
            parameter with { Min = -1 },
            parameter with { Max = 11 },
            parameter with { Name = "other" },
            parameter with { IsRequired = false },
            parameter with { Type = "string" },
        ];

        foreach (var changed in changedParameters)
        {
            Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, TestScripts.Create(changed)));
        }
    }

    [TestMethod]
    public void ChoiceChangesAndParameterReorderingInvalidateEnteredValues()
    {
        var first = TestScripts.Parameter("mode", "choice", options: ["Fast", "Safe"]);
        var second = TestScripts.Parameter("text");
        var previous = TestScripts.Create(first, second);

        Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, TestScripts.Create(first with { Options = ["Fast", "New"] }, second)));
        Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, TestScripts.Create(second, first)));
        Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, TestScripts.Create(first)));
    }

    [TestMethod]
    [DataRow("title")]
    [DataRow("description")]
    public void ParameterNamesThatMatchAnnotationKeywordsStillHaveTheirConstraintsCompared(string name)
    {
        var previous = SchemaScript(name, """{"type":"string","minLength":1}""");
        var current = SchemaScript(name, """{"type":"string","minLength":2}""");

        Assert.IsFalse(PowerScriptsService.HasSameParameterContract(previous, current));
    }

    private static PowerScriptInfo SchemaScript(string name, string propertySchema) => new()
    {
        Parameters = [TestScripts.Parameter(name, required: true)],
        InputSchema = TestScripts.Json("{\"type\":\"object\",\"properties\":{\"parameters\":{\"type\":\"object\",\"properties\":{\"" + name + "\":" + propertySchema + "}}}}"),
    };
}
