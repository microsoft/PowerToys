// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ParameterContractTests
{
    [TestMethod]
    public void Resolver_AppliesDefaults_AndValidatesRequiredValues()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Parameters =
            [
                new ScriptParameter { Name = "required", IsRequired = true },
                new ScriptParameter { Name = "count", Type = "int", Default = "3", Min = 1, Max = 5 },
            ],
        };

        Assert.IsFalse(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?>(),
            out _,
            out var missingError));
        StringAssert.Contains(missingError, "required");

        Assert.IsTrue(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["required"] = "value" },
            out var resolved,
            out _));
        Assert.AreEqual("value", resolved["required"]);
        Assert.AreEqual("3", resolved["count"]);
    }

    [TestMethod]
    public void Resolver_RejectsUnknownAndInvalidValues()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Parameters =
            [
                new ScriptParameter
                {
                    Name = "mode",
                    Type = "choice",
                    Options = ["safe", "fast"],
                },
            ],
        };

        Assert.IsFalse(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["unknown"] = "value" },
            out _,
            out var unknownError));
        StringAssert.Contains(unknownError, "Unknown parameter");

        Assert.IsFalse(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["mode"] = "invalid" },
            out _,
            out var invalidError));
        StringAssert.Contains(invalidError, "safe, fast");
    }

    [TestMethod]
    public void Resolver_MatchesNamesCaseInsensitively_AndRejectsEmptyTypedValues()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Parameters =
            [
                new ScriptParameter { Name = "enabled", Type = "bool" },
            ],
        };

        Assert.IsTrue(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["ENABLED"] = "true" },
            out var resolved,
            out _));
        Assert.AreEqual("true", resolved["enabled"]);

        Assert.IsFalse(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["enabled"] = string.Empty },
            out _,
            out var emptyError));
        StringAssert.Contains(emptyError, "true or false");
    }

    [TestMethod]
    public void Resolver_FileParameter_RequiresExistingFile()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Parameters =
            [
                new ScriptParameter
                {
                    Name = "file",
                    Type = ScriptParameter.ParameterTypeFile,
                    IsRequired = true,
                },
            ],
        };

        Assert.IsFalse(ScriptParameterResolver.TryResolve(
            manifest,
            new Dictionary<string, string?> { ["file"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) },
            out _,
            out var missingError));
        StringAssert.Contains(missingError, "existing file");

        var existingFile = Path.GetTempFileName();
        try
        {
            Assert.IsTrue(ScriptParameterResolver.TryResolve(
                manifest,
                new Dictionary<string, string?> { ["file"] = existingFile },
                out var resolved,
                out _));
            Assert.AreEqual(existingFile, resolved["file"]);
        }
        finally
        {
            File.Delete(existingFile);
        }
    }

    [TestMethod]
    public void ExecutionContext_Grants_FileParameter_Path_To_Mxc()
    {
        var file = Path.GetTempFileName();
        try
        {
            var manifest = new PowerScriptManifest
            {
                Id = "demo",
                Parameters =
                [
                    new ScriptParameter { Name = "file", Type = ScriptParameter.ParameterTypeFile },
                    new ScriptParameter { Name = "label", Type = ScriptParameter.ParameterTypeString },
                ],
            };

            var context = ProcessExecutionContext.FromManifest(
                manifest,
                parameters: new Dictionary<string, string?>
                {
                    ["file"] = file,
                    ["label"] = Path.GetTempPath(),
                });

            CollectionAssert.AreEqual(new[] { file }, context.InputPaths.ToArray());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void Descriptor_MapsJsonSchemaRequiredArray()
    {
        var root = Path.Combine(Path.GetTempPath(), "powerscripts-params-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "demo.ps1");
            File.WriteAllText(script, "Write-Output ok");
            var descriptor = script + ToolDescriptorParser.DescriptorSuffix;
            File.WriteAllText(descriptor, """
                {
                  "name": "demo",
                  "inputSchema": {
                    "type": "object",
                    "properties": {
                      "requiredValue": { "type": "string" },
                      "optionalValue": { "type": "string" }
                    },
                    "required": ["requiredValue"]
                  }
                }
                """);

            var manifest = ToolDescriptorParser.TryParseFile(descriptor);
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Parameters.Single(parameter => parameter.Name == "requiredValue").IsRequired);
            Assert.IsFalse(manifest.Parameters.Single(parameter => parameter.Name == "optionalValue").IsRequired);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
