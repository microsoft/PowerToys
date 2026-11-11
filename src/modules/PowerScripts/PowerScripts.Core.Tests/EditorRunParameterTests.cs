// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class EditorRunParameterTests
{
    [TestMethod]
    public void ExplicitFalseZeroAndEmptyStringRemainTypedWhileOmittedDefaultsStayOmitted()
    {
        var script = Script(
            Parameter("flag", "bool", defaultValue: "true"),
            Parameter("count", "int", defaultValue: "3"),
            Parameter("note", defaultValue: "default"),
            Parameter("omitted", defaultValue: "preserved"));

        Assert.IsTrue(PowerScriptParameterValues.TryCreate(
            script,
            new Dictionary<string, string?> { ["flag"] = "false", ["count"] = "0", ["note"] = string.Empty },
            out var values,
            out var error));
        Assert.IsNull(error);
        Assert.AreEqual(3, values.Count);
        Assert.AreEqual(JsonValueKind.False, values["flag"].ValueKind);
        Assert.AreEqual(0, values["count"].GetInt32());
        Assert.AreEqual(string.Empty, values["note"].GetString());
        Assert.IsFalse(values.ContainsKey("omitted"));
    }

    [TestMethod]
    public void RequiredValueCanBeOmittedOnlyWhenTheHostAdvertisesADefault()
    {
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("name", required: true)),
            new Dictionary<string, string?>(),
            out var missing,
            out var missingError));
        Assert.AreEqual(0, missing.Count);
        StringAssert.Contains(missingError!, "name");

        Assert.IsTrue(PowerScriptParameterValues.TryCreate(
            Script(Parameter("name", required: true, defaultValue: "World")),
            new Dictionary<string, string?>(),
            out var omitted,
            out _));
        Assert.AreEqual(0, omitted.Count);
    }

    [DataTestMethod]
    [DataRow("string", "")]
    [DataRow("string", " \t\r\n")]
    [DataRow("file", "")]
    [DataRow("file", "   ")]
    public void RequiredTextOrFileCannotBeBlank(string type, string input)
    {
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("value", type, required: true)),
            new Dictionary<string, string?> { ["value"] = input },
            out _,
            out var error));
        StringAssert.Contains(error!, "required");
    }

    [TestMethod]
    public void FileValuesAreLiteralStringsAndHostOwnsFileAccessValidation()
    {
        const string file = "C:\\literal %PATH%\\\"quoted\"\\日本語.txt";
        Assert.IsTrue(PowerScriptParameterValues.TryCreate(
            Script(Parameter("file", "file", required: true)),
            new Dictionary<string, string?> { ["file"] = file },
            out var values,
            out _));
        Assert.AreEqual(file, values["file"].GetString());
    }

    [TestMethod]
    public void SuppliedNamesMatchHostCaseRulesAndOutputUsesCanonicalNames()
    {
        Assert.IsTrue(PowerScriptParameterValues.TryCreate(
            Script(Parameter("count", "int")),
            new Dictionary<string, string?> { ["COUNT"] = "2" },
            out var values,
            out _));
        Assert.AreEqual(2, values["count"].GetInt32());
        Assert.IsFalse(values.ContainsKey("COUNT"));
    }

    [TestMethod]
    public void UnknownOrRepeatedNamesAreRejectedWithoutPartialOutput()
    {
        var script = Script(Parameter("count", "int"));
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            script,
            new Dictionary<string, string?> { ["count"] = "2", ["unknown"] = "3" },
            out var unknown,
            out var unknownError));
        Assert.AreEqual(0, unknown.Count);
        StringAssert.Contains(unknownError!, "Unknown parameter");

        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            script,
            new Dictionary<string, string?> { ["count"] = "2", ["COUNT"] = "3" },
            out var repeated,
            out var repeatedError));
        Assert.AreEqual(0, repeated.Count);
        StringAssert.Contains(repeatedError!, "more than once");
    }

    [TestMethod]
    public void RepeatedDeclarationNamesAreRejectedUsingHostCaseRules()
    {
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("name"), Parameter("NAME")),
            new Dictionary<string, string?>(),
            out _,
            out var error));
        StringAssert.Contains(error!, "duplicate");
    }

    [DataTestMethod]
    [DataRow("bool", "")]
    [DataRow("bool", "yes")]
    [DataRow("int", "1.5")]
    [DataRow("int", "1e2")]
    [DataRow("int", "2147483648")]
    [DataRow("int", "")]
    public void InvalidPrimitiveTextIsRejected(string type, string input)
    {
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("value", type)),
            new Dictionary<string, string?> { ["value"] = input },
            out _,
            out _));
    }

    [TestMethod]
    public void IntegerBoundsUseInvariantWholeNumbers()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var script = Script(Parameter("count", "int", min: -2, max: 1000));
            Assert.IsTrue(PowerScriptParameterValues.TryCreate(
                script,
                new Dictionary<string, string?> { ["count"] = "1000" },
                out var values,
                out _));
            Assert.AreEqual(1000, values["count"].GetInt32());
            Assert.IsFalse(PowerScriptParameterValues.TryCreate(
                script,
                new Dictionary<string, string?> { ["count"] = "1,000" },
                out _,
                out _));
            Assert.IsFalse(PowerScriptParameterValues.TryCreate(
                script,
                new Dictionary<string, string?> { ["count"] = "-3" },
                out _,
                out var minimumError));
            StringAssert.Contains(minimumError!, "at least -2");
            Assert.IsFalse(PowerScriptParameterValues.TryCreate(
                script,
                new Dictionary<string, string?> { ["count"] = "1001" },
                out _,
                out var maximumError));
            StringAssert.Contains(maximumError!, "at most 1000");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    public void ChoicesMatchAdvertisedValuesExactly()
    {
        var script = Script(Parameter("mode", "choice", options: ["safe", "fast"]));
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            script,
            new Dictionary<string, string?> { ["mode"] = "FAST" },
            out _,
            out var error));
        StringAssert.Contains(error!, "safe, fast");
        Assert.IsTrue(PowerScriptParameterValues.TryCreate(
            script,
            new Dictionary<string, string?> { ["mode"] = "fast" },
            out var values,
            out _));
        Assert.AreEqual("fast", values["mode"].GetString());
    }

    [TestMethod]
    public void NullValuesAndUnsupportedTypesAreRejected()
    {
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("name")),
            new Dictionary<string, string?> { ["name"] = null },
            out _,
            out var nullError));
        StringAssert.Contains(nullError!, "null");
        Assert.IsFalse(PowerScriptParameterValues.TryCreate(
            Script(Parameter("name", "object")),
            new Dictionary<string, string?> { ["name"] = "{}" },
            out _,
            out var typeError));
        StringAssert.Contains(typeError!, "unsupported type");
    }

    [TestMethod]
    public void ContractIgnoresPresentationAndParameterOrder()
    {
        var first = Parameter("first");
        var second = Parameter("second", "int", defaultValue: "3");
        var previous = Script(first, second);
        var current = Script(second with { Label = "Count", Description = "A new label" }, first);
        Assert.IsTrue(PowerScriptParameterValues.HasSameInputContract(previous, current));
    }

    [TestMethod]
    public void ContractDetectsTypeRequiredDefaultRangeAndChoiceChanges()
    {
        var original = Parameter("value", "int", defaultValue: "3", min: 1, max: 5);
        var previous = Script(original);
        foreach (var changed in new[]
        {
            original with { Type = "string" },
            original with { IsRequired = true },
            original with { Default = "4" },
            original with { Min = 2 },
            original with { Max = 4 },
            original with { Options = ["3"] },
        })
        {
            Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(previous, Script(changed)));
        }
    }

    [TestMethod]
    public void ContractDetectsPrimaryInputChanges()
    {
        Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(
            new PowerScriptInfo { Id = "demo", Input = PowerScriptIO.None },
            new PowerScriptInfo { Id = "demo", Input = PowerScriptIO.Text }));
    }

    [TestMethod]
    public void SchemaObjectOrderAndAnnotationsDoNotChangeTheContract()
    {
        var previous = WithSchema(
            """{"title":"Old","type":"object","properties":{"parameters":{"type":"object","properties":{"name":{"type":"string","title":"Old label","description":"Old help","maxLength":10}}}}}""",
            Parameter("name"));
        var current = WithSchema(
            """{"properties":{"parameters":{"properties":{"name":{"maxLength":10.0,"description":"New help","title":"New label","type":"string"}},"type":"object"}},"type":"object","title":"New"}""",
            Parameter("name"));
        Assert.IsTrue(PowerScriptParameterValues.HasSameInputContract(previous, current));
    }

    [TestMethod]
    public void SchemaConstraintsArePartOfTheContract()
    {
        var previous = WithSchema(
            """{"type":"object","properties":{"parameters":{"type":"object","properties":{"name":{"type":"string","maxLength":10}}}}}""",
            Parameter("name"));
        var current = WithSchema(
            """{"type":"object","properties":{"parameters":{"type":"object","properties":{"name":{"type":"string","maxLength":20}}}}}""",
            Parameter("name"));
        Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(previous, current));
    }

    [TestMethod]
    public void PropertiesNamedTitleOrDescriptionAreNotAnnotations()
    {
        var previous = WithSchema(
            """{"type":"object","properties":{"parameters":{"type":"object","properties":{"description":{"type":"string","maxLength":10},"title":{"type":"string"}}}}}""",
            Parameter("description"), Parameter("title"));
        var changed = WithSchema(
            """{"type":"object","properties":{"parameters":{"type":"object","properties":{"description":{"type":"string","maxLength":20},"title":{"type":"string"}}}}}""",
            Parameter("description"), Parameter("title"));
        Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(previous, changed));
    }

    [TestMethod]
    public void MissingOrDuplicateSchemaPropertiesCannotMatchAnUnambiguousContract()
    {
        var previous = WithSchema("""{"type":"object","properties":{"parameters":{"type":"object"}}}""");
        Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(previous, Script()));
        var duplicate = WithSchema("""{"type":"object","properties":{"name":{"type":"string"},"name":{"type":"string"}}}""");
        Assert.IsFalse(PowerScriptParameterValues.HasSameInputContract(duplicate, duplicate));
    }

    private static PowerScriptParameter Parameter(
        string name,
        string type = "string",
        bool required = false,
        string? defaultValue = null,
        IReadOnlyList<string>? options = null,
        int? min = null,
        int? max = null) =>
        new(name, type, required, null, null, defaultValue, options ?? [], min, max);

    private static PowerScriptInfo Script(params PowerScriptParameter[] parameters) =>
        new() { Id = "demo", Parameters = parameters };

    private static PowerScriptInfo WithSchema(string schema, params PowerScriptParameter[] parameters)
    {
        using var document = JsonDocument.Parse(schema);
        return new PowerScriptInfo { Id = "demo", Parameters = parameters, InputSchema = document.RootElement.Clone() };
    }
}
