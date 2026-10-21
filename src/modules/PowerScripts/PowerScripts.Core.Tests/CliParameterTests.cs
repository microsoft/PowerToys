// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliParameterTests
{
    [TestMethod]
    public void RequiredParameter_RejectsMissingNullAndEmptyValues()
    {
        var command = CreateDefinition(new ScriptParameter { Name = "name", IsRequired = true });

        AssertRejected(command, new Dictionary<string, string?>());
        AssertRejected(command, new Dictionary<string, string?> { ["name"] = null });
        AssertRejected(command, new Dictionary<string, string?> { ["name"] = string.Empty });
    }

    [TestMethod]
    public void Defaults_AreAppliedWithoutOverwritingSuppliedValues()
    {
        var command = CreateDefinition(
            new ScriptParameter { Name = "name", IsRequired = true },
            new ScriptParameter { Name = "count", Type = "int", Default = "3", Min = 1, Max = 5 },
            new ScriptParameter { Name = "enabled", Type = "bool", Default = "false" });

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["NAME"] = "中文名称" },
            out var defaults,
            out var defaultError), defaultError);
        Assert.AreEqual("中文名称", defaults["name"]);
        Assert.AreEqual("3", defaults["count"]);
        Assert.AreEqual("false", defaults["enabled"]);

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["name"] = "Override", ["count"] = "5", ["enabled"] = "true" },
            out var supplied,
            out var suppliedError), suppliedError);
        Assert.AreEqual("Override", supplied["name"]);
        Assert.AreEqual("5", supplied["count"]);
        Assert.AreEqual("true", supplied["enabled"]);
    }

    [TestMethod]
    public void UnknownParameter_IsRejected()
    {
        var command = CreateDefinition(new ScriptParameter { Name = "name" });

        AssertRejected(command, new Dictionary<string, string?> { ["unexpected"] = "value" });
    }

    [TestMethod]
    public void ParameterNamesDifferingOnlyByCase_AreRejectedWhenBothAreSupplied()
    {
        var command = CreateDefinition(new ScriptParameter { Name = "count", Type = "int" });
        var supplied = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["count"] = "1",
            ["COUNT"] = "2",
        };

        AssertRejected(command, supplied);
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("5")]
    public void IntegerParameter_AcceptsInclusiveBounds(string value)
    {
        var command = CreateDefinition(new ScriptParameter { Name = "count", Type = "int", Min = 1, Max = 5 });

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["count"] = value },
            out var resolved,
            out var error), error);
        Assert.AreEqual(value, resolved["count"]);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("6")]
    [DataRow("3.5")]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    [DataRow("invalid")]
    [DataRow("")]
    public void IntegerParameter_RejectsOutOfRangeAndMalformedValues(string value)
    {
        var command = CreateDefinition(new ScriptParameter { Name = "count", Type = "int", Min = 1, Max = 5 });

        AssertRejected(command, new Dictionary<string, string?> { ["count"] = value });
    }

    [DataTestMethod]
    [DataRow("+03", "3")]
    [DataRow(" -02 ", "-2")]
    public void IntegerParameter_NormalizesAcceptedValues(string value, string expected)
    {
        var command = CreateDefinition(new ScriptParameter { Name = "count", Type = "int" });

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["count"] = value },
            out var resolved,
            out var error), error);
        Assert.AreEqual(expected, resolved["count"]);
    }

    [DataTestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    [DataRow("TRUE", true)]
    public void BooleanParameter_AcceptsBooleanValues(string value, bool expected)
    {
        var command = CreateDefinition(new ScriptParameter { Name = "enabled", Type = "bool" });

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["enabled"] = value },
            out var resolved,
            out var error), error);
        Assert.AreEqual(expected ? "true" : "false", resolved["enabled"]);
    }

    [DataTestMethod]
    [DataRow("yes")]
    [DataRow("1")]
    [DataRow("on")]
    [DataRow("")]
    public void BooleanParameter_RejectsValuesThatAreNotBooleans(string value)
    {
        var command = CreateDefinition(new ScriptParameter { Name = "enabled", Type = "bool" });

        AssertRejected(command, new Dictionary<string, string?> { ["enabled"] = value });
    }

    [TestMethod]
    public void ChoiceParameter_RequiresAnExactDeclaredOption()
    {
        var command = CreateDefinition(new ScriptParameter
        {
            Name = "mode",
            Type = "choice",
            Options = ["safe", "fast"],
        });

        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["mode"] = "safe" },
            out var resolved,
            out var error), error);
        Assert.AreEqual("safe", resolved["mode"]);
        AssertRejected(command, new Dictionary<string, string?> { ["mode"] = "SAFE" });
        AssertRejected(command, new Dictionary<string, string?> { ["mode"] = "other" });
        AssertRejected(command, new Dictionary<string, string?> { ["mode"] = string.Empty });
    }

    [TestMethod]
    public void FileParameter_AcceptsAnExistingAbsoluteFilePath()
    {
        var file = Path.GetTempFileName();
        try
        {
            var command = CreateFileCommand();

            Assert.IsTrue(command.TryResolveParameters(
                new Dictionary<string, string?> { ["path"] = file },
                out var resolved,
                out var error), error);
            Assert.AreEqual(Path.GetFullPath(file), resolved["path"]);
            Assert.IsTrue(Path.IsPathFullyQualified(resolved["path"]!));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void FileParameter_RejectsMissingFilesAndDirectories()
    {
        var command = CreateFileCommand();
        var missingFile = Path.Combine(Path.GetTempPath(), "powerscripts-cli-missing-" + Guid.NewGuid().ToString("N"));

        AssertRejected(command, new Dictionary<string, string?> { ["path"] = missingFile });
        AssertRejected(command, new Dictionary<string, string?> { ["path"] = Path.GetTempPath() });
    }

    [TestMethod]
    public void FileParameter_NormalizesAnExistingRelativePath()
    {
        var relativePath = Path.Combine(".", "powerscripts-cli-relative-" + Guid.NewGuid().ToString("N") + ".txt");
        var fullPath = Path.GetFullPath(relativePath);
        File.WriteAllText(fullPath, "test input");
        try
        {
            Assert.IsTrue(CreateFileCommand().TryResolveParameters(
                new Dictionary<string, string?> { ["path"] = relativePath },
                out var resolved,
                out var error), error);
            Assert.AreEqual(fullPath, resolved["path"]);
        }
        finally
        {
            File.Delete(fullPath);
        }
    }

    [TestMethod]
    public void OptionalFileParameter_CanBeOmittedOrEmpty()
    {
        var command = CreateDefinition(new ScriptParameter { Name = "path", Type = ScriptParameter.ParameterTypeFile });

        Assert.IsTrue(command.TryResolveParameters(new Dictionary<string, string?>(), out _, out var missingError), missingError);
        Assert.IsTrue(command.TryResolveParameters(
            new Dictionary<string, string?> { ["path"] = string.Empty },
            out _,
            out var emptyError), emptyError);
    }

    [TestMethod]
    public void ParameterValueContainingNul_IsRejected()
    {
        var command = CreateDefinition(new ScriptParameter { Name = "name" });

        AssertRejected(command, new Dictionary<string, string?> { ["name"] = "before\0after" });
        AssertRejected(CreateFileCommand(), new Dictionary<string, string?> { ["path"] = "invalid\0file.txt" });
    }

    private static BuiltinCliDefinition CreateFileCommand() => CreateDefinition(new ScriptParameter
    {
        Name = "path",
        Type = ScriptParameter.ParameterTypeFile,
        IsRequired = true,
    });

    private static BuiltinCliDefinition CreateDefinition(params ScriptParameter[] parameters) => new()
    {
        Id = "test.command",
        Name = "Test command",
        Group = "Tests",
        Description = "A native command used to validate its input contract.",
        Executable = "FileLocksmithCLI.exe",
        Parameters = parameters,
        Bindings = parameters.Select(parameter => new CliArgumentBinding
        {
            Parameter = parameter.Name,
            Kind = CliArgumentKind.Option,
            Name = "--" + parameter.Name,
        }).ToArray(),
    };

    private static void AssertRejected(BuiltinCliDefinition command, IReadOnlyDictionary<string, string?> supplied)
    {
        Assert.IsFalse(command.TryResolveParameters(supplied, out _, out var error));
        Assert.IsFalse(string.IsNullOrWhiteSpace(error), "Rejected inputs must include a useful error.");
    }
}
