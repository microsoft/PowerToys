// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptParameterValuesTests
{
    [TestMethod]
    public void DefaultsKeepFalseZeroAndEmptyStringAsExplicitValues()
    {
        var script = TestScripts.Create(
            TestScripts.Parameter("enabled", "bool", defaultValue: "False"),
            TestScripts.Parameter("count", "int", defaultValue: "0"),
            TestScripts.Parameter("text", defaultValue: string.Empty),
            TestScripts.Parameter("omitted"));

        var input = PowerScriptParameterValues.CreateInitialValues(script);
        var valid = PowerScriptParameterValues.TryRead(script, input, out var values, out var errors);

        Assert.IsTrue(valid);
        Assert.HasCount(0, errors);
        Assert.HasCount(3, values);
        Assert.AreEqual(JsonValueKind.False, values["enabled"].ValueKind);
        Assert.AreEqual(0, values["count"].GetInt32());
        Assert.AreEqual(string.Empty, values["text"].GetString());
        Assert.IsFalse(values.ContainsKey("omitted"));
        Assert.AreEqual("false", input["parameter_0"]);
    }

    [TestMethod]
    public void OptionalUncheckedFieldsAreOmittedEvenWhenTheirTextIsInvalid()
    {
        var script = TestScripts.Create(TestScripts.Parameter("count", "int", defaultValue: "7"));
        var input = new Dictionary<string, string>
        {
            ["include_0"] = "false",
            ["parameter_0"] = "not an integer",
        };

        Assert.IsTrue(PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        Assert.HasCount(0, values);
        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RequiredValueMustBePresentButMayBeAnEmptyString()
    {
        var script = TestScripts.Create(TestScripts.Parameter("text", required: true));

        Assert.IsFalse(PowerScriptParameterValues.TryRead(script, new Dictionary<string, string>(), out _, out var missingErrors));
        Assert.IsTrue(missingErrors.ContainsKey("parameter_0"));

        var input = new Dictionary<string, string> { ["parameter_0"] = string.Empty };
        Assert.IsTrue(PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        Assert.HasCount(0, errors);
        Assert.AreEqual(string.Empty, values["text"].GetString());
    }

    [TestMethod]
    [DataRow("-2", true)]
    [DataRow("0", true)]
    [DataRow("2", true)]
    [DataRow("-3", false)]
    [DataRow("3", false)]
    [DataRow("2147483648", false)]
    [DataRow("-2147483649", false)]
    [DataRow("1.0", false)]
    [DataRow("1,000", false)]
    [DataRow("", false)]
    public void IntegerBoundsAndSyntaxAreValidated(string text, bool expected)
    {
        var script = TestScripts.Create(TestScripts.Parameter("count", "int", required: true, min: -2, max: 2));
        var input = new Dictionary<string, string> { ["parameter_0"] = text };

        Assert.AreEqual(expected, PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        if (expected)
        {
            Assert.AreEqual(JsonValueKind.Number, values["count"].ValueKind);
            Assert.HasCount(0, errors);
        }
        else
        {
            Assert.IsFalse(values.ContainsKey("count"));
            Assert.IsTrue(errors.ContainsKey("parameter_0"));
        }
    }

    [TestMethod]
    public void IntegerParsingDoesNotUseTheDisplayCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var script = TestScripts.Create(TestScripts.Parameter("count", "int", required: true));
            var input = new Dictionary<string, string> { ["parameter_0"] = "-1234" };

            Assert.IsTrue(PowerScriptParameterValues.TryRead(script, input, out var values, out _));
            Assert.AreEqual(-1234, values["count"].GetInt32());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow("true", true, true)]
    [DataRow("FALSE", true, false)]
    [DataRow("0", false, false)]
    [DataRow("yes", false, false)]
    [DataRow("", false, false)]
    public void BooleanValuesAreTypedAndInvalidValuesAreRejected(string text, bool expected, bool boolean)
    {
        var script = TestScripts.Create(TestScripts.Parameter("enabled", "bool", required: true));
        var input = new Dictionary<string, string> { ["parameter_0"] = text };

        Assert.AreEqual(expected, PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        if (expected)
        {
            Assert.AreEqual(boolean, values["enabled"].GetBoolean());
        }
        else
        {
            Assert.IsTrue(errors.ContainsKey("parameter_0"));
        }
    }

    [TestMethod]
    [DataRow("Fast", true)]
    [DataRow("fast", false)]
    [DataRow("custom", false)]
    public void ChoicesRequireAnExactDeclaredOption(string text, bool expected)
    {
        var script = TestScripts.Create(TestScripts.Parameter("mode", "choice", required: true, options: ["Fast", "Safe"]));
        var input = new Dictionary<string, string> { ["parameter_0"] = text };

        Assert.AreEqual(expected, PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        if (expected)
        {
            Assert.AreEqual("Fast", values["mode"].GetString());
        }
        else
        {
            Assert.IsTrue(errors.ContainsKey("parameter_0"));
        }
    }

    [TestMethod]
    public void FileParametersRequireAnExistingFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var script = TestScripts.Create(TestScripts.Parameter("source", "file", required: true));
            var input = new Dictionary<string, string> { ["parameter_0"] = path };

            Assert.IsTrue(PowerScriptParameterValues.TryRead(script, input, out var values, out _));
            Assert.AreEqual(path, values["source"].GetString());

            File.Delete(path);
            Assert.IsFalse(PowerScriptParameterValues.TryRead(script, input, out _, out var errors));
            Assert.IsTrue(errors.ContainsKey("parameter_0"));

            input["parameter_0"] = Path.GetTempPath();
            Assert.IsFalse(PowerScriptParameterValues.TryRead(script, input, out _, out errors));
            Assert.IsTrue(errors.ContainsKey("parameter_0"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow("😀", true)]
    [DataRow("中", true)]
    [DataRow("", false)]
    [DataRow("😀😀", false)]
    public void StringLengthUsesUnicodeScalars(string text, bool expected)
    {
        var script = StringSchemaScript("""
            {"type":"object","properties":{"parameters":{"type":"object","properties":{"value":{"type":"string","minLength":1,"maxLength":1}}}}}
            """);
        var input = new Dictionary<string, string> { ["parameter_0"] = text };

        Assert.AreEqual(expected, PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        if (expected)
        {
            Assert.AreEqual(text, values["value"].GetString());
        }
        else
        {
            Assert.IsTrue(errors.ContainsKey("parameter_0"));
        }
    }

    [TestMethod]
    public void PatternValidationRejectsNonMatchingAndMalformedPatterns()
    {
        var matchingScript = StringSchemaScript("""
            {"properties":{"parameters":{"properties":{"value":{"type":"string","pattern":"^[A-Z]{2}$"}}}}}
            """);
        var input = new Dictionary<string, string> { ["parameter_0"] = "AB" };
        Assert.IsTrue(PowerScriptParameterValues.TryRead(matchingScript, input, out _, out _));

        input["parameter_0"] = "lowercase";
        Assert.IsFalse(PowerScriptParameterValues.TryRead(matchingScript, input, out _, out var mismatchErrors));
        Assert.IsTrue(mismatchErrors.ContainsKey("parameter_0"));

        var malformedScript = StringSchemaScript("""
            {"properties":{"parameters":{"properties":{"value":{"type":"string","pattern":"["}}}}}
            """);
        Assert.IsFalse(PowerScriptParameterValues.TryRead(malformedScript, input, out _, out var malformedErrors));
        Assert.IsTrue(malformedErrors.ContainsKey("parameter_0"));
    }

    [TestMethod]
    public void UnknownFieldsCannotInjectAdditionalScriptParameters()
    {
        var script = TestScripts.Create(TestScripts.Parameter("safe", required: true));
        var input = new Dictionary<string, string>
        {
            ["parameter_0"] = "preserved",
            ["parameter_1"] = "unexpected",
            ["unknown"] = "unexpected",
            ["safe"] = "must not override the indexed field",
        };

        Assert.IsTrue(PowerScriptParameterValues.TryRead(script, input, out var values, out _));
        Assert.HasCount(1, values);
        Assert.AreEqual("preserved", values["safe"].GetString());
    }

    [TestMethod]
    public void InvalidOptionalIncludeValueIsAValidationError()
    {
        var script = TestScripts.Create(TestScripts.Parameter("value"));
        var input = new Dictionary<string, string> { ["include_0"] = "yes", ["parameter_0"] = "text" };

        Assert.IsFalse(PowerScriptParameterValues.TryRead(script, input, out var values, out var errors));
        Assert.HasCount(0, values);
        Assert.IsTrue(errors.ContainsKey("parameter_0"));
    }

    [TestMethod]
    public void UnknownParameterTypeIsNotSilentlySubmittedAsAString()
    {
        var script = TestScripts.Create(TestScripts.Parameter("value", "unknown", required: true));
        var input = new Dictionary<string, string> { ["parameter_0"] = "text" };

        Assert.IsFalse(PowerScriptParameterValues.TryRead(script, input, out _, out var errors));
        Assert.IsTrue(errors.ContainsKey("parameter_0"));
    }

    // A fixed seed and bounded corpus make this user-input fuzz regression reproducible in unit-test runs.
    [TestMethod]
    public void BoundedRandomInputsPreserveStringDataAndTypedParameterInvariants()
    {
        var random = new Random(0x5C71);
        string[] tokens = ["\\", "\"", "\0", "\n", "\r", "\t", "中文", "😀", "false", "true", "0", "1", "-", " ", "x"];
        var script = TestScripts.Create(
            TestScripts.Parameter("text", required: true),
            TestScripts.Parameter("count", "int", required: true, min: -100, max: 100),
            TestScripts.Parameter("flag", "bool", required: true),
            TestScripts.Parameter("omitted", "int"));

        for (var iteration = 0; iteration < 512; iteration++)
        {
            var builder = new StringBuilder();
            var count = random.Next(17);
            for (var index = 0; index < count; index++)
            {
                builder.Append(tokens[random.Next(tokens.Length)]);
            }

            var text = builder.ToString();
            var input = new Dictionary<string, string>
            {
                ["parameter_0"] = text,
                ["parameter_1"] = iteration % 3 == 0 ? random.Next(-100, 101).ToString(CultureInfo.InvariantCulture) : text,
                ["parameter_2"] = iteration % 3 == 0 ? "false" : text,
                ["include_3"] = "false",
                ["parameter_3"] = text,
                ["injected"] = text,
            };

            var valid = PowerScriptParameterValues.TryRead(script, input, out var values, out var errors);

            Assert.AreEqual(text, values["text"].GetString(), $"String data changed at corpus entry {iteration}.");
            Assert.IsFalse(values.ContainsKey("omitted"));
            Assert.IsFalse(values.ContainsKey("injected"));
            Assert.IsTrue(valid || errors.Count > 0);
            Assert.IsTrue(errors.Keys.All(key => key is "parameter_1" or "parameter_2"));
            if (values.TryGetValue("count", out var number))
            {
                Assert.AreEqual(JsonValueKind.Number, number.ValueKind);
                Assert.IsTrue(number.GetInt32() is >= -100 and <= 100);
            }

            if (values.TryGetValue("flag", out var flag))
            {
                Assert.IsTrue(flag.ValueKind is JsonValueKind.True or JsonValueKind.False);
            }

            if (iteration % 3 == 0)
            {
                Assert.IsTrue(valid, $"Valid typed values were rejected at corpus entry {iteration}.");
            }
        }
    }

    private static PowerScriptInfo StringSchemaScript(string schema) => new()
    {
        Id = "schema.test",
        Input = PowerScriptIO.None,
        Output = PowerScriptIO.Text,
        Parameters = [TestScripts.Parameter("value", required: true)],
        InputSchema = TestScripts.Json(schema),
    };
}
