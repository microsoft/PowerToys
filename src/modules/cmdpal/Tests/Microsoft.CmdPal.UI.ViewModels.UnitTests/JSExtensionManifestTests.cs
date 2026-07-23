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

    [TestMethod]
    public void TryParse_PublisherFromAuthorString_WhenNoCmdPalPublisher()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "author-string",
            "main": "index.js",
            "author": "Jane Doe <jane@example.com> (https://example.com)",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("Jane Doe", result.Manifest!.Publisher);
    }

    [TestMethod]
    public void TryParse_PublisherFromAuthorObject_WhenNoCmdPalPublisher()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "author-object",
            "main": "index.js",
            "author": { "name": "Acme Corp", "email": "dev@acme.example", "url": "https://acme.example" },
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("Acme Corp", result.Manifest!.Publisher);
    }

    [TestMethod]
    public void TryParse_CmdPalPublisher_TakesPrecedenceOverAuthor()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "precedence",
            "main": "index.js",
            "author": "Author Name",
            "cmdpal": { "publisher": "cmdpal-publisher" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("cmdpal-publisher", result.Manifest!.Publisher);
    }

    [TestMethod]
    public void TryParse_NoPublisherAndNoAuthor_LeavesPublisherNull()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "no-publisher",
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.IsNull(result.Manifest!.Publisher);
    }

    private void CreateEntryPoint(string relativePath)
    {
        var fullPath = Path.Combine(_testDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, "// entry point");
    }

    [TestMethod]
    public void TryParse_UnknownFields_AreIgnored()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "unknown-fields",
            "main": "index.js",
            "keywords": ["a", "b"],
            "scripts": { "build": "tsc" },
            "futureTopLevel": 42,
            "cmdpal": {
                "displayName": "Has Unknowns",
                "futureCmdPalField": { "nested": true }
            }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("unknown-fields", result.Manifest!.Name);
        Assert.AreEqual("Has Unknowns", result.Manifest.DisplayName);
    }

    [TestMethod]
    public void TryParse_MalformedValueType_IsInvalid()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "malformed-type",
            "main": "index.js",
            "cmdpal": {
                "debug": "not-a-boolean"
            }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Manifest);
    }

    [TestMethod]
    public void TryParse_MjsEntryPoint_IsValid()
    {
        CreateEntryPoint("index.mjs");
        const string Json = """
        {
            "name": "esm-sample",
            "main": "index.mjs",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual(Path.Combine(_testDirectory, "index.mjs"), result.Manifest!.EntryPointPath);
    }

    [TestMethod]
    public void TryParse_CjsEntryPoint_IsValid()
    {
        CreateEntryPoint("index.cjs");
        const string Json = """
        {
            "name": "cjs-sample",
            "main": "index.cjs",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual(Path.Combine(_testDirectory, "index.cjs"), result.Manifest!.EntryPointPath);
    }

    [TestMethod]
    public void TryParse_UnsupportedEntryPointExtension_IsInvalid()
    {
        CreateEntryPoint("index.ts");
        const string Json = """
        {
            "name": "typescript-source",
            "main": "index.ts",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.FailureReason, "JavaScript file");
    }

    [TestMethod]
    public void TryParse_NameKey_IsNormalizedIdentity()
    {
        CreateEntryPoint("index.js");
        const string Json = """
        {
            "name": "  MixedCase-Name  ",
            "main": "index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("mixedcase-name", result.Manifest!.NameKey);
    }

    [TestMethod]
    public void TryParse_EntryPointThroughJunction_IsRejected()
    {
        // Create a directory outside the extension dir with a real entry point, then expose it inside
        // the extension dir through a junction. A lexically-contained path must still be rejected
        // because it traverses a reparse point that redirects outside the package.
        var outsideDirectory = Path.Combine(Path.GetTempPath(), $"JSExtensionManifestJunctionTarget_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(Path.Combine(outsideDirectory, "index.js"), "// entry point");

        var junctionPath = Path.Combine(_testDirectory, "linked");
        if (!TryCreateJunction(junctionPath, outsideDirectory))
        {
            Directory.Delete(outsideDirectory, recursive: true);
            Assert.Inconclusive("A directory junction could not be created in this environment.");
            return;
        }

        try
        {
            const string Json = """
            {
                "name": "junction-escape",
                "main": "linked/index.js",
                "cmdpal": {}
            }
            """;

            var result = JSExtensionManifest.TryParse(Json, _testDirectory);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.FailureReason, "junction");
        }
        finally
        {
            // Remove the junction reparse point itself (non-recursive) before deleting the target so
            // the shared cleanup does not try to recurse through the junction into the outside tree.
            if (Directory.Exists(junctionPath))
            {
                Directory.Delete(junctionPath, recursive: false);
            }

            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void TryParse_RelativeIcon_ResolvesToContainedAbsolutePath()
    {
        CreateEntryPoint("dist/index.js");
        CreateEntryPoint("assets/icon.png");
        const string Json = """
        {
            "name": "relative-icon",
            "main": "dist/index.js",
            "cmdpal": { "icon": "assets/icon.png" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        var expected = Path.GetFullPath(Path.Combine(_testDirectory, "assets", "icon.png"));
        Assert.AreEqual(expected, result.Manifest!.IconPath);
    }

    [TestMethod]
    public void TryParse_RootDirectory_IsResolvedToPackageRoot()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "root-directory",
            "main": "dist/index.js",
            "cmdpal": {}
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_testDirectory));
        Assert.AreEqual(expected, result.Manifest!.RootDirectory);
    }

    [TestMethod]
    public void TryParse_RelativeIcon_ThatEscapesPackage_ResolvesToEmpty()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "escaping-icon",
            "main": "dist/index.js",
            "cmdpal": { "icon": "../outside-icon.png" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual(string.Empty, result.Manifest!.IconPath);
    }

    [TestMethod]
    public void TryParse_RelativeIcon_ThatDoesNotExist_ResolvesToEmpty()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "missing-icon",
            "main": "dist/index.js",
            "cmdpal": { "icon": "assets/missing.png" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual(string.Empty, result.Manifest!.IconPath);
    }

    [TestMethod]
    public void TryParse_GlyphIcon_IsPreservedUnchanged()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "glyph-icon",
            "main": "dist/index.js",
            "cmdpal": { "icon": "\uE700" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("\uE700", result.Manifest!.IconPath);
    }

    [TestMethod]
    public void TryParse_UriIcon_IsPreservedUnchanged()
    {
        CreateEntryPoint("dist/index.js");
        const string Json = """
        {
            "name": "uri-icon",
            "main": "dist/index.js",
            "cmdpal": { "icon": "https://example.com/icon.png" }
        }
        """;

        var result = JSExtensionManifest.TryParse(Json, _testDirectory);

        Assert.IsTrue(result.IsValid, result.FailureReason);
        Assert.AreEqual("https://example.com/icon.png", result.Manifest!.IconPath);
    }

    [TestMethod]
    public void TryParse_IconThroughJunction_ResolvesToEmpty()
    {
        // An icon whose lexical path stays inside the package but traverses a junction that
        // redirects outside the package must resolve to empty rather than load the outside file.
        CreateEntryPoint("dist/index.js");

        var outsideDirectory = Path.Combine(Path.GetTempPath(), $"JSExtensionIconJunctionTarget_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(Path.Combine(outsideDirectory, "icon.png"), "// icon bytes");

        var junctionPath = Path.Combine(_testDirectory, "linked-assets");
        if (!TryCreateJunction(junctionPath, outsideDirectory))
        {
            Directory.Delete(outsideDirectory, recursive: true);
            Assert.Inconclusive("A directory junction could not be created in this environment.");
            return;
        }

        try
        {
            const string Json = """
            {
                "name": "junction-icon",
                "main": "dist/index.js",
                "cmdpal": { "icon": "linked-assets/icon.png" }
            }
            """;

            var result = JSExtensionManifest.TryParse(Json, _testDirectory);

            Assert.IsTrue(result.IsValid, result.FailureReason);
            Assert.AreEqual(string.Empty, result.Manifest!.IconPath);
        }
        finally
        {
            if (Directory.Exists(junctionPath))
            {
                Directory.Delete(junctionPath, recursive: false);
            }

            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    private static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0 && Directory.Exists(junctionPath);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
