// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.UI.ViewModels;

namespace ViewModelTests;

[TestClass]
public class PowerScriptOperationDraftTests
{
    private static readonly string[] BooleanChoiceLabels = { "JSON", "Text" };
    private static readonly string[] StringChoiceLabels = { "fast", "full" };

    [TestMethod]
    public void RequiredFileMustBeConfiguredBeforeSavingOrTesting()
    {
        var draft = Create("path", """{"type":"string","format":"file-path","title":"File"}""", required: true);
        var parameter = draft.Parameters[0];

        Assert.IsTrue(parameter.IsFile);
        Assert.IsTrue(parameter.IsValueProvided);
        Assert.IsFalse(parameter.CanOmit);
        Assert.IsFalse(draft.TryBuildConfiguration(out _, out _, out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error));
        Assert.IsFalse(draft.TryGetTestValues(out _, out _));

        parameter.IsValueProvided = false;
        parameter.TextValue = "  ";

        Assert.IsTrue(parameter.IsValueProvided, "A required value cannot be omitted.");
        Assert.IsFalse(draft.TryBuildConfiguration(out _, out _, out _));

        parameter.TextValue = @"C:\work\notes.txt";

        Assert.IsTrue(draft.TryBuildConfiguration(out var parameters, out var operation, out _));
        Assert.AreEqual(0, parameters.Count);
        Assert.AreEqual(parameter.TextValue, operation["bindings"]["path"]["constant"].GetValue<string>());
        Assert.IsTrue(draft.TryGetTestValues(out var values, out _));
        Assert.AreEqual(0, values.Count);
    }

    [TestMethod]
    [DataRow("""{"type":"boolean","default":false}""", "false")]
    [DataRow("""{"type":"boolean","default":true}""", "true")]
    [DataRow("""{"type":"integer","default":0}""", "0")]
    [DataRow("""{"type":"string","default":""}""", "\"\"")]
    [DataRow("""{"type":"string","enum":["fast","full"],"default":"fast"}""", "\"fast\"")]
    public void SchemaDefaultsPrepopulateValuesThatCannotBeOmitted(string schema, string expectedJson)
    {
        var draft = Create("value", schema);
        var parameter = draft.Parameters[0];

        Assert.IsTrue(parameter.HasDefault);
        Assert.IsTrue(parameter.IsValueProvided);
        Assert.IsFalse(parameter.CanOmit);
        parameter.IsValueProvided = false;

        Assert.IsTrue(parameter.IsValueProvided, "The configured default is part of the fixed operation.");
        Assert.IsTrue(draft.TryBuildConfiguration(out var parameters, out var operation, out _));
        Assert.AreEqual(0, parameters.Count);
        Assert.AreEqual(expectedJson, operation["bindings"]["value"]["constant"].ToJsonString());
    }

    [TestMethod]
    [DataRow("""{"type":"boolean"}""", "", "false")]
    [DataRow("""{"type":"integer"}""", "0", "0")]
    [DataRow("""{"type":"string"}""", "", "\"\"")]
    public void OptionalPresenceKeepsFalseZeroAndEmptyStringSeparateFromOmission(string schema, string text, string expectedJson)
    {
        var draft = Create("value", schema);
        var parameter = draft.Parameters[0];
        parameter.TextValue = text;

        Assert.IsTrue(parameter.CanOmit);
        Assert.IsFalse(parameter.IsValueProvided);
        Assert.IsTrue(draft.TryBuildConfiguration(out _, out var omitted, out _));
        Assert.AreEqual(0, omitted["bindings"].AsObject().Count);

        parameter.IsValueProvided = true;

        Assert.IsTrue(draft.TryBuildConfiguration(out var parameters, out var configured, out _));
        Assert.AreEqual(0, parameters.Count);
        Assert.AreEqual(expectedJson, configured["bindings"]["value"]["constant"].ToJsonString());

        parameter.IsValueProvided = false;

        Assert.IsTrue(draft.TryBuildConfiguration(out _, out omitted, out _));
        Assert.AreEqual(0, omitted["bindings"].AsObject().Count);

        parameter.IsValueProvided = true;

        Assert.IsTrue(draft.TryBuildConfiguration(out _, out configured, out _));
        Assert.AreEqual(expectedJson, configured["bindings"]["value"]["constant"].ToJsonString());
    }

    [TestMethod]
    public void FriendlyBooleanChoicesWriteRealBooleansAndIgnoreInitialNullSelection()
    {
        var draft = Create("json", """{"type":"boolean","default":false,"x-ui":{"control":"choice","trueLabel":"JSON","falseLabel":"Text"}}""");
        var parameter = draft.Parameters[0];
        var changed = new List<string>();
        parameter.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        Assert.IsTrue(parameter.IsBooleanChoice);
        Assert.IsFalse(parameter.IsBooleanToggle);
        Assert.IsFalse(parameter.IsChoice);
        CollectionAssert.AreEqual(BooleanChoiceLabels, new List<string>(parameter.BooleanChoices));
        Assert.AreEqual("Text", parameter.SelectedBooleanChoice);

        parameter.SelectedBooleanChoice = null;

        Assert.AreEqual("Text", parameter.SelectedBooleanChoice);
        Assert.IsFalse(parameter.BooleanValue);

        parameter.SelectedBooleanChoice = "JSON";

        Assert.IsTrue(parameter.BooleanValue);
        Assert.IsTrue(draft.TryBuildConfiguration(out _, out var operation, out _));
        Assert.IsTrue(operation["bindings"]["json"]["constant"].GetValue<bool>());

        parameter.BooleanValue = false;

        Assert.AreEqual("Text", parameter.SelectedBooleanChoice);
        CollectionAssert.Contains(changed, nameof(PowerScriptOperationParameter.SelectedBooleanChoice));
        Assert.IsTrue(draft.TryBuildConfiguration(out _, out operation, out _));
        Assert.IsFalse(operation["bindings"]["json"]["constant"].GetValue<bool>());
    }

    [TestMethod]
    public void PlainBooleanUsesAToggle()
    {
        var parameter = Create("enabled", """{"type":"boolean","default":false}""").Parameters[0];

        Assert.IsTrue(parameter.IsBooleanToggle);
        Assert.IsFalse(parameter.IsBooleanChoice);
        Assert.AreEqual(0, parameter.BooleanChoices.Count);
        Assert.IsNull(parameter.SelectedBooleanChoice);
    }

    [TestMethod]
    [DataRow(null, "Text")]
    [DataRow("", "Text")]
    [DataRow(" ", "Text")]
    [DataRow("JSON", "")]
    [DataRow("JSON", "JSON")]
    public void BooleanChoiceLabelsMustBeDistinctAndNonEmpty(string trueLabel, string falseLabel)
    {
        var schema = new JsonObject
        {
            ["type"] = "boolean",
            ["x-ui"] = new JsonObject { ["control"] = "choice", ["trueLabel"] = trueLabel, ["falseLabel"] = falseLabel },
        };

        Assert.ThrowsException<ArgumentException>(() => new PowerScriptOperationParameter(
            new PowerScriptOperationTemplateParameter { Name = "json", Schema = schema }));
    }

    [TestMethod]
    public void StringChoicesRemainTypedStringsAndEnforceTheirContract()
    {
        var draft = Create("mode", """{"type":"string","enum":["fast","full"],"default":"fast"}""");
        var parameter = draft.Parameters[0];

        Assert.IsTrue(parameter.IsChoice);
        Assert.IsFalse(parameter.IsBooleanChoice);
        Assert.AreEqual("fast", parameter.TextValue);
        CollectionAssert.AreEqual(StringChoiceLabels, new List<string>(parameter.Choices));

        parameter.TextValue = "unknown";

        Assert.IsFalse(draft.TryBuildConfiguration(out _, out _, out _));
        Assert.IsFalse(draft.TryGetTestValues(out _, out _));

        parameter.TextValue = "full";

        Assert.IsTrue(draft.TryBuildConfiguration(out _, out var operation, out _));
        Assert.AreEqual("full", operation["bindings"]["mode"]["constant"].GetValue<string>());
    }

    [TestMethod]
    [DataRow("not a number")]
    [DataRow("-1")]
    [DataRow("11")]
    public void InvalidIntegersBlockCreationAndTestingEqually(string text)
    {
        var draft = Create("count", """{"type":"integer","minimum":0,"maximum":10,"default":5}""");
        draft.Parameters[0].TextValue = text;

        Assert.IsFalse(draft.TryBuildConfiguration(out _, out _, out var createError));
        Assert.IsFalse(draft.TryGetTestValues(out var values, out var testError));
        Assert.AreEqual(createError, testError);
        Assert.IsFalse(string.IsNullOrEmpty(createError));
        Assert.AreEqual(0, values.Count);
    }

    [TestMethod]
    public void TestingUsesTheFixedConfigurationWithoutRuntimeParameters()
    {
        var draft = new PowerScriptOperationDraft(new PowerScriptOperationTemplate
        {
            CommandId = "sample.operation",
            Parameters = new()
            {
                Definition("path", """{"type":"string","format":"file-path"}""", required: true),
                Definition("json", """{"type":"boolean","default":false}"""),
                Definition("count", """{"type":"integer"}"""),
            },
        });
        draft.Parameters[0].TextValue = @"C:\work\notes.txt";
        draft.Parameters[2].IsValueProvided = true;
        draft.Parameters[2].TextValue = "0";

        Assert.IsTrue(draft.TryBuildConfiguration(out var parameters, out var operation, out _));
        Assert.IsTrue(draft.TryGetTestValues(out var values, out _));
        Assert.AreEqual(0, parameters.Count);
        Assert.AreEqual(0, values.Count);
        Assert.AreEqual(3, operation["bindings"].AsObject().Count);
        Assert.AreEqual(draft.Parameters[0].TextValue, operation["bindings"]["path"]["constant"].GetValue<string>());
        Assert.IsFalse(operation["bindings"]["json"]["constant"].GetValue<bool>());
        Assert.AreEqual(0, operation["bindings"]["count"]["constant"].GetValue<int>());

        draft.Parameters[0].TextValue = string.Empty;

        Assert.IsFalse(draft.TryGetTestValues(out _, out _));
        Assert.IsFalse(draft.TryBuildConfiguration(out _, out _, out _));
    }

    [TestMethod]
    public void SchemaSnapshotIsUnaffectedByLaterCatalogueMutation()
    {
        var definition = Definition("count", """{"type":"integer","minimum":1,"maximum":10,"default":2}""");
        var parameter = new PowerScriptOperationParameter(definition);
        definition.Schema["minimum"] = 100;
        definition.Schema["default"] = 9;

        Assert.AreEqual("2", parameter.TextValue);
        Assert.IsTrue(parameter.TryValidateForCreate(out _));
        Assert.AreEqual(2, parameter.ToBinding()["constant"].GetValue<int>());

        parameter.TextValue = "5";

        Assert.IsTrue(parameter.TryValidateForCreate(out _), "Validation must use the original catalogue snapshot.");
    }

    [TestMethod]
    public void ParameterlessOperationsStillProduceAValidFixedConfiguration()
    {
        var draft = new PowerScriptOperationDraft(new PowerScriptOperationTemplate { CommandId = "sample.operation" });

        Assert.IsTrue(draft.TryBuildConfiguration(out var parameters, out var operation, out _));
        Assert.AreEqual(0, parameters.Count);
        Assert.AreEqual(0, operation["bindings"].AsObject().Count);
        Assert.IsTrue(draft.TryGetTestValues(out var values, out _));
        Assert.AreEqual(0, values.Count);
    }

    [TestMethod]
    public void DuplicateParameterNamesAndUnsupportedTypesAreRejectedBeforeRendering()
    {
        var template = new PowerScriptOperationTemplate
        {
            CommandId = "sample.operation",
            Parameters = new()
            {
                Definition("value", """{"type":"string"}"""),
                Definition("VALUE", """{"type":"string"}"""),
            },
        };

        Assert.ThrowsException<ArgumentException>(() => new PowerScriptOperationDraft(template));
        Assert.ThrowsException<ArgumentException>(() => Create("value", """{"type":"object"}"""));
    }

    private static PowerScriptOperationDraft Create(string name, string schema, bool required = false) => new(
        new PowerScriptOperationTemplate
        {
            Id = "powertoys:sample.operation",
            CommandId = "sample.operation",
            Name = "Sample operation",
            Parameters = new() { Definition(name, schema, required) },
        });

    private static PowerScriptOperationTemplateParameter Definition(string name, string schema, bool required = false) => new()
    {
        Name = name,
        Schema = JsonNode.Parse(schema).AsObject(),
        Required = required,
    };
}
