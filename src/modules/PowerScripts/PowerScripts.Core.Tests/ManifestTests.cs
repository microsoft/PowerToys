// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

[TestClass]
public class ManifestTests
{
    [TestMethod]
    public void Serializer_RoundTrips_WithCamelCaseEnums()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Name = "Demo",
            Kind = ScriptKind.File,
            Runtime = ScriptRuntime.PowerShell,
            Entry = "run.ps1",
            Input = new ScriptInput { Extensions = { ".png" }, MinFiles = 1, MaxFiles = 0 },
            Output = new ScriptOutput { Type = ScriptOutputType.SideEffect },
            Surfaces = { "contextMenu" },
        };

        var json = ManifestSerializer.Serialize(manifest);
        StringAssert.Contains(json, "\"kind\": \"file\"");
        StringAssert.Contains(json, "\"runtime\": \"powerShell\"");

        var back = ManifestSerializer.Deserialize(json);
        Assert.IsNotNull(back);
        Assert.AreEqual(ScriptKind.File, back!.Kind);
        Assert.AreEqual(ScriptOutputType.SideEffect, back.Output!.Type);
        Assert.AreEqual(".png", back.Input!.Extensions[0]);
    }

    [TestMethod]
    public void Validator_Allows_IdFolderMismatch()
    {
        // A script's id is portable and intentionally decoupled from its folder name, so a mismatch
        // is no longer an error (a downloaded/shared script keeps its id in any folder).
        var manifest = new PowerScriptManifest { Id = "abc", Name = "x", Entry = "run.ps1" };
        var errors = ManifestValidator.Validate(manifest, folderName: "different");
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Validator_Flags_MissingId()
    {
        var manifest = new PowerScriptManifest { Id = string.Empty, Name = "x", Entry = "run.ps1" };
        var errors = ManifestValidator.Validate(manifest, folderName: "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("'id' is required")));
    }

    [TestMethod]
    public void Validator_Flags_IdWithUnsafeCharacters()
    {
        var manifest = new PowerScriptManifest { Id = "my script", Name = "x", Entry = "run.ps1" };
        var errors = ManifestValidator.Validate(manifest, folderName: "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("'id' may only contain")));
    }

    [TestMethod]
    public void Validator_Allows_IdWithSafeCharacters()
    {
        var manifest = new PowerScriptManifest { Id = "py_greet-1.0", Name = "x", Entry = "run.ps1" };
        var errors = ManifestValidator.Validate(manifest, folderName: "abc");
        Assert.IsFalse(errors.Any(e => e.Contains("'id'")));
    }

    [TestMethod]
    public void Validator_Flags_FileKind_WithoutExtensions()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            Kind = ScriptKind.File,
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("input.extensions")));
    }

    [TestMethod]
    public void Validator_Flags_MaxFiles_LessThanMin()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            Kind = ScriptKind.File,
            Input = new ScriptInput { Extensions = { ".png" }, MinFiles = 3, MaxFiles = 2 },
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("maxFiles")));
    }

    [TestMethod]
    public void Serializer_RoundTrips_ChoiceParameter_WithOptions()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "demo",
            Name = "Demo",
            Entry = "run.ps1",
            PromptForParameters = true,
            Parameters =
            {
                new ScriptParameter
                {
                    Name = "greeting",
                    Type = ScriptParameter.ParameterTypeChoice,
                    Label = "Greeting",
                    Options = { "Hello", "Hi" },
                    Default = "Hello",
                },
            },
        };

        var json = ManifestSerializer.Serialize(manifest);
        StringAssert.Contains(json, "\"promptForParameters\": true");
        StringAssert.Contains(json, "\"type\": \"choice\"");

        var back = ManifestSerializer.Deserialize(json);
        Assert.IsNotNull(back);
        Assert.IsTrue(back!.PromptForParameters);
        var p = back.Parameters.Single();
        Assert.IsTrue(p.IsChoice);
        CollectionAssert.AreEqual(new[] { "Hello", "Hi" }, p.Options);
        Assert.AreEqual("Greeting", p.DisplayLabel);
    }

    [TestMethod]
    public void Validator_Flags_ChoiceParameter_WithoutOptions()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            Parameters = { new ScriptParameter { Name = "mode", Type = ScriptParameter.ParameterTypeChoice } },
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("must declare at least one 'options'")));
    }

    [TestMethod]
    public void Validator_Flags_ChoiceDefault_NotInOptions()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            Parameters =
            {
                new ScriptParameter
                {
                    Name = "mode",
                    Type = ScriptParameter.ParameterTypeChoice,
                    Options = { "a", "b" },
                    Default = "c",
                },
            },
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("not one of its options")));
    }

    [TestMethod]
    public void Validator_Flags_UnknownParameterType()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            Parameters = { new ScriptParameter { Name = "p", Type = "date" } },
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.IsTrue(errors.Any(e => e.Contains("unknown type")));
    }

    [TestMethod]
    public void Validator_Allows_ValidParameters()
    {
        var manifest = new PowerScriptManifest
        {
            Id = "abc",
            Name = "x",
            Entry = "run.ps1",
            PromptForParameters = true,
            Parameters =
            {
                new ScriptParameter { Name = "greeting", Type = ScriptParameter.ParameterTypeChoice, Options = { "Hi" }, Default = "Hi" },
                new ScriptParameter { Name = "name", Type = ScriptParameter.ParameterTypeString, Default = "World" },
                new ScriptParameter { Name = "count", Type = ScriptParameter.ParameterTypeInt, Min = 1, Max = 5, Default = "2" },
                new ScriptParameter { Name = "shout", Type = ScriptParameter.ParameterTypeBool, Default = "false" },
            },
        };

        var errors = ManifestValidator.Validate(manifest, "abc");
        Assert.AreEqual(0, errors.Count);
    }
}
