// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class JSExtensionManifestTests
{
    private static readonly string[] ExpectedCapabilities = ["commands"];

    private string _testDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JSExtensionManifestTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void TryParse_ValidManifest_PopulatesAllFields()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "@publisher/cmdpal-sample",
            "version": "2.3.4",
            "description": "A sample extension",
            "main": "dist/index.js",
            "engines": { "node": ">=18" },
            "cmdpal": {
                "displayName": "Sample Extension",
                "icon": "icon.png",
                "publisher": "sample-publisher",
                "capabilities": ["commands"],
                "debug": true,
                "debugPort": 9230
            }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        var manifest = result.Manifest!;
        Assert.AreEqual("@publisher/cmdpal-sample", manifest.Name);
        Assert.AreEqual("2.3.4", manifest.Version);
        Assert.AreEqual("A sample extension", manifest.Description);
        Assert.AreEqual("dist/index.js", manifest.Main);
        Assert.AreEqual("Sample Extension", manifest.DisplayName);
        Assert.AreEqual("icon.png", manifest.Icon);
        Assert.AreEqual("sample-publisher", manifest.Publisher);
        Assert.IsNotNull(manifest.Capabilities);
        CollectionAssert.AreEqual(ExpectedCapabilities, manifest.Capabilities);
        Assert.IsTrue(manifest.Debug);
        Assert.AreEqual(9230, manifest.DebugPort);
        Assert.AreEqual(">=18", manifest.Engines!.Node);
        Assert.AreEqual(Path.Combine(_testDirectory, "dist", "index.js"), manifest.EntryPointPath);
    }

    [TestMethod]
    public void TryParse_CmdPalMain_OverridesTopLevelMain()
    {
        CreateEntryPoint("dist/cmdpal-entry.js");
        const string Json = """
        {
            "name": "override-sample",
            "main": "dist/index.js",
            "cmdpal": {
                "main": "dist/cmdpal-entry.js"
            }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("dist/cmdpal-entry.js", result.Manifest!.Main);
        Assert.AreEqual(Path.Combine(_testDirectory, "dist", "cmdpal-entry.js"), result.Manifest.EntryPointPath);
    }

    [TestMethod]
    public void TryParse_EmptyCmdPalSection_WithTopLevelMain_IsValid()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "minimal",
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("minimal", result.Manifest!.Name);
    }

    [TestMethod]
    public void TryParse_MissingCmdPalSection_IsInvalid()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "not-an-extension",
            "main": "index.js"
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Manifest);
        StringAssert.Contains(result.FailureReason, "cmdpal");
    }

    [TestMethod]
    public void TryParse_MissingName_IsInvalid()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "name");
    }

    [TestMethod]
    public void TryParse_EmptyName_IsInvalid()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "   ",
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "name");
    }

    [TestMethod]
    public void TryParse_NoEntryPointSpecified_IsInvalid()
    {
        const string Json = """
        {
            "name": "no-main",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "main");
    }

    [TestMethod]
    public void TryParse_EntryPointFileMissing_IsInvalid()
    {
        const string Json = """
        {
            "name": "missing-entry",
            "main": "dist/index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "does not resolve");
    }

    [TestMethod]
    public void TryParse_InvalidJson_IsInvalid()
    {
        const string Json = "{ this is not valid json";

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "valid JSON");
    }

    [TestMethod]
    public void TryParse_EffectiveDisplayName_FallsBackToName()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "fallback-name",
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.IsNull(result.Manifest!.DisplayName);
        Assert.AreEqual("fallback-name", result.Manifest.EffectiveDisplayName);
    }

    [TestMethod]
    public void TryParseFile_ReadsAndValidatesPackageJson()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "from-file",
            "main": "dist/index.js",
            "cmdpal": { "displayName": "From File" }
        }
        """;
        var packageJsonPath = Path.Combine(_testDirectory, "package.json");
        File.WriteAllText(packageJsonPath, Json);

        var result = JSExtensionManifest.TryParseFile(packageJsonPath);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("from-file", result.Manifest!.Name);
        Assert.AreEqual("From File", result.Manifest.DisplayName);
    }

    [TestMethod]
    public void TryParseFile_MissingFile_IsInvalid()
    {
        var packageJsonPath = Path.Combine(_testDirectory, "package.json");

        var result = JSExtensionManifest.TryParseFile(packageJsonPath);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "package.json");
    }

    [TestMethod]
    public void TryParse_AbsoluteEntryPoint_IsInvalid()
    {
        // Create a real file at an absolute path outside the extension directory so the
        // rejection is due to the rooted-path rule, not a missing file.
        var outsidePath = Path.Combine(Path.GetTempPath(), $"JSExtensionManifestAbsolute_{Guid.NewGuid():N}.js");
        File.WriteAllText(outsidePath, "// entry point");
        try
        {
            var json = $$"""
            {
                "name": "absolute-entry",
                "main": {{System.Text.Json.JsonSerializer.Serialize(outsidePath)}},
                "cmdpal": {}
            }
            """;

            var result = JSExtensionManifest.TryParse(json, _testDirectory);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.FailureReason, "relative path");
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [TestMethod]
    public void TryParse_EntryPointEscapingDirectory_IsInvalid()
    {
        // Create a real file one level above the extension directory and try to reach it via "..".
        var parent = Path.GetDirectoryName(_testDirectory)!;
        var escapeTarget = Path.Combine(parent, $"JSExtensionManifestEscape_{Guid.NewGuid():N}.js");
        File.WriteAllText(escapeTarget, "// entry point");
        try
        {
            var relative = "../" + Path.GetFileName(escapeTarget);
            var json = $$"""
            {
                "name": "escaping-entry",
                "main": {{System.Text.Json.JsonSerializer.Serialize(relative)}},
                "cmdpal": {}
            }
            """;

            var result = JSExtensionManifest.TryParse(json, _testDirectory);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.FailureReason, "escape");
        }
        finally
        {
            File.Delete(escapeTarget);
        }
    }

    private void CreateEntryPoint(string relativePath)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, "// entry point");
    }
}
