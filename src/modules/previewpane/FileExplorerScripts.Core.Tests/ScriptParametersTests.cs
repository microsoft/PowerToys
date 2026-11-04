// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class ScriptParametersTests
{
    [TestMethod]
    public void DefaultsStayTypedAndUnsetOptionalValuesAreOmitted()
    {
        using var fixture = new Fixture();
        var script = Script(
            Parameter("int", "12", required: true, min: 1, max: 20),
            Parameter("bool", "false"),
            Parameter("choice", "one", options: ["one", "two"]),
            Parameter("file", fixture.Input),
            Parameter("string", null));
        var values = ScriptParameters.GetDefaults(script);
        Assert.AreEqual(12, values["int"].GetInt32());
        Assert.IsFalse(values["bool"].GetBoolean());
        Assert.AreEqual("one", values["choice"].GetString());
        Assert.AreEqual(fixture.Input, values["file"].GetString());
        Assert.IsFalse(values.ContainsKey("string"));
        Assert.IsTrue(ScriptParameters.CanRunWithDefaults(script));
        Assert.AreEqual(0, ScriptParameters.Parse(Script(Parameter("string", "default")), new Dictionary<string, string?> { ["string"] = null }).Count);
    }

    [TestMethod]
    public void MissingRequiredAndInvalidDefaultsDisableDefaultExecution()
    {
        foreach (var parameter in new[]
        {
            Parameter("string", null, required: true),
            Parameter("int", "1.5"),
            Parameter("int", "30", min: 1, max: 20),
            Parameter("bool", "yes"),
            Parameter("choice", "three", options: ["one", "two"]),
            Parameter("choice", null),
            Parameter("file", @"C:\missing-powerscripts-fixture.txt"),
            Parameter("other", null),
        })
        {
            var script = Script(parameter);
            Assert.IsFalse(ScriptParameters.CanRunWithDefaults(script), parameter.Type);
            var error = Assert.Throws<ArgumentException>(() => ScriptParameters.GetDefaults(script));
            StringAssert.Contains(error.Message, "Friendly");
        }

        Assert.ThrowsExactly<ArgumentNullException>(() => ScriptParameters.CanRunWithDefaults(null!));
    }

    [DataTestMethod]
    [DataRow("int", " 3")]
    [DataRow("int", "3 ")]
    [DataRow("int", "1.5")]
    [DataRow("int", "2147483648")]
    [DataRow("bool", "1")]
    [DataRow("bool", "TRUE")]
    [DataRow("bool", "true ")]
    [DataRow("choice", "ONE")]
    [DataRow("string", " ")]
    public void ParseRejectsInvalidExplicitValuesWithoutCoercion(string type, string value)
    {
        var script = Script(Parameter(type, null, required: true, options: ["one"]));
        var error = Assert.Throws<ArgumentException>(() => ScriptParameters.Parse(script, new Dictionary<string, string?> { [type] = value }));
        StringAssert.Contains(error.Message, "Friendly");
    }

    [TestMethod]
    public void BoundsChoicesUnknownNamesAndFilesAreValidated()
    {
        using var fixture = new Fixture();
        var script = Script(Parameter("int", null, min: -2, max: 2), Parameter("choice", null, options: ["one", "two"]));
        foreach (var boundary in new[] { "-2", "2" })
        {
            Assert.AreEqual(int.Parse(boundary, System.Globalization.CultureInfo.InvariantCulture), ScriptParameters.Parse(script, new Dictionary<string, string?> { ["int"] = boundary })["int"].GetInt32());
        }

        Assert.Throws<ArgumentException>(() => ScriptParameters.Parse(script, new Dictionary<string, string?> { ["int"] = "3" }));
        Assert.Throws<ArgumentException>(() => ScriptParameters.Parse(script, new Dictionary<string, string?> { ["missing"] = "value" }));
        Assert.Throws<ArgumentException>(() => ScriptParameters.GetDefaults(Script(Parameter("int", null, min: 2, max: 1))));
        Assert.Throws<ArgumentException>(() => ScriptParameters.GetDefaults(Script(Parameter("file", fixture.Destination))));
        Assert.Throws<ArgumentException>(() => ScriptParameters.GetDefaults(Script(Parameter("int", "1"), Parameter("int", "2"))));
        Assert.AreEqual(JsonValueKind.String, ScriptParameters.Parse(script, new Dictionary<string, string?> { ["choice"] = "two" })["choice"].ValueKind);
    }

    [TestMethod]
    public void FuzzParameterTextRejectsMalformedValuesWithoutUnexpectedExceptions()
    {
        var random = new Random(7103);
        foreach (var type in new[] { "int", "bool", "choice", "string", "file" })
        {
            var script = Script(Parameter(type, null, options: ["one", "two"]));
            for (var iteration = 0; iteration < 200; iteration++)
            {
                var text = new string(Enumerable.Range(0, random.Next(1, 80)).Select(_ => (char)random.Next(0, 256)).ToArray());
                try
                {
                    var values = ScriptParameters.Parse(script, new Dictionary<string, string?> { [type] = text });
                    Assert.AreEqual(type == "int" ? JsonValueKind.Number : type == "bool" ? JsonValueKind.True : JsonValueKind.String, values[type].ValueKind);
                }
                catch (ArgumentException)
                {
                }
            }
        }
    }

    [TestMethod]
    public void FuzzParameterMetadataAndValuesValidateDefaultsAndRejectCorruptedContracts()
    {
        using var fixture = new Fixture();
        var random = new Random(19013);
        for (var iteration = 0; iteration < 180; iteration++)
        {
            var name = "parameter" + random.Next(100000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var label = "Visible label " + name;
            var minimum = random.Next(-1000, 1000);
            var maximum = minimum + random.Next(1, 1000);
            var number = random.Next(minimum, maximum + 1);
            var type = (iteration % 5) switch
            {
                0 => "int",
                1 => "bool",
                2 => "choice",
                3 => "string",
                _ => "file",
            };
            var value = type switch
            {
                "int" => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "bool" => random.Next(2) == 0 ? "true" : "false",
                "choice" => "option-" + name,
                "file" => fixture.Input,
                _ => "text-" + name,
            };
            var parameter = new PowerScriptParameter(
                name,
                type,
                random.Next(2) == 0,
                label,
                null,
                value,
                type == "choice" ? [value, "other-option"] : [],
                type == "int" ? minimum : null,
                type == "int" ? maximum : null);
            var script = Script(parameter);
            Assert.IsTrue(ScriptParameters.CanRunWithDefaults(script));
            var defaults = ScriptParameters.GetDefaults(script);
            var parsed = ScriptParameters.Parse(script, new Dictionary<string, string?> { [name] = value });
            Assert.AreEqual(defaults[name].GetRawText(), parsed[name].GetRawText());
            if (type == "int")
            {
                Assert.AreEqual(number, parsed[name].GetInt32());
            }
            else if (type == "bool")
            {
                Assert.AreEqual(value == "true", parsed[name].GetBoolean());
            }
            else
            {
                Assert.AreEqual(value, parsed[name].GetString());
            }

            var corrupted = (iteration % 9) switch
            {
                0 => parameter with { Name = new string(' ', random.Next(0, 8)) },
                1 => parameter with { Type = "unknown-" + name },
                2 => parameter with { Type = "int", Min = maximum + 1, Max = minimum },
                3 => parameter with { Type = "choice", Options = [] },
                4 => parameter with { Type = "int", Default = "2147483648", Min = null, Max = null },
                5 => parameter with { Type = "bool", Default = value + " " },
                6 => parameter with { Type = "choice", Default = "undeclared", Options = ["one", "two"] },
                7 => parameter with { IsRequired = true, Default = null },
                _ => parameter,
            };
            var invalid = iteration % 9 == 8 ? Script(corrupted, corrupted) : Script(corrupted);
            Assert.IsFalse(ScriptParameters.CanRunWithDefaults(invalid));
            var error = Assert.Throws<ArgumentException>(() => ScriptParameters.GetDefaults(invalid));
            StringAssert.Contains(error.Message, label);
            error = Assert.Throws<ArgumentException>(() => ScriptParameters.Parse(invalid, new Dictionary<string, string?>()));
            StringAssert.Contains(error.Message, label);
        }
    }

    private static PowerScriptInfo Script(params PowerScriptParameter[] parameters) => new() { Parameters = parameters };

    private static PowerScriptParameter Parameter(string type, string? value, bool required = false, int? min = null, int? max = null, IReadOnlyList<string>? options = null) =>
        new(type, type, required, "Friendly " + type, null, value, options ?? [], min, max);
}
