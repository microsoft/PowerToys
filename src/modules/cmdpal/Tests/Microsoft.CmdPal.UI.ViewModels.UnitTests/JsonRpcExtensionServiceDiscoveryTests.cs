// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class JsonRpcExtensionServiceDiscoveryTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"JSExtDiscovery_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void DiscoverManifests_ReturnsOnlyValidCmdPalExtensions()
    {
        // Valid: has cmdpal section + resolvable entry point.
        const string GoodJson = """
        {
            "name": "good-ext",
            "main": "index.js",
            "cmdpal": { "displayName": "Good" }
        }
        """;
        CreateExtension("good", GoodJson, "index.js");

        // Invalid: no cmdpal section.
        const string NoCmdPalJson = """
        {
            "name": "plain-ext",
            "main": "index.js"
        }
        """;
        CreateExtension("no-cmdpal", NoCmdPalJson, "index.js");

        // Invalid: cmdpal section but entry point does not exist.
        const string MissingEntryJson = """
        {
            "name": "missing-entry-ext",
            "main": "index.js",
            "cmdpal": {}
        }
        """;
        CreateExtension("missing-entry", MissingEntryJson, entryPointRelativePath: null);

        // Invalid: directory without a package.json at all.
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        var results = JsonRpcExtensionService.DiscoverManifests(_root);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("good-ext", results[0].Manifest.Name);
        Assert.AreEqual(Path.Combine(_root, "good"), results[0].Directory);
    }

    [TestMethod]
    public void DiscoverManifests_MissingRoot_ReturnsEmpty()
    {
        var results = JsonRpcExtensionService.DiscoverManifests(Path.Combine(_root, "does-not-exist"));
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void DiscoverManifests_EmptyRoot_ReturnsEmpty()
    {
        var results = JsonRpcExtensionService.DiscoverManifests(_root);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void DecideCrashAction_AtOrBelowLimit_Restarts()
    {
        Assert.AreEqual(JsonRpcExtensionService.CrashAction.Restart, JsonRpcExtensionService.DecideCrashAction(1, 3));
        Assert.AreEqual(JsonRpcExtensionService.CrashAction.Restart, JsonRpcExtensionService.DecideCrashAction(2, 3));
        Assert.AreEqual(JsonRpcExtensionService.CrashAction.Restart, JsonRpcExtensionService.DecideCrashAction(3, 3));
    }

    [TestMethod]
    public void DecideCrashAction_AboveLimit_Disables()
    {
        Assert.AreEqual(JsonRpcExtensionService.CrashAction.Disable, JsonRpcExtensionService.DecideCrashAction(4, 3));
        Assert.AreEqual(JsonRpcExtensionService.CrashAction.Disable, JsonRpcExtensionService.DecideCrashAction(10, 3));
    }

    [TestMethod]
    public void IsUnderDirectory_SamePath_IsTrue()
    {
        var dir = Path.Combine(_root, "foo");
        Assert.IsTrue(JsonRpcExtensionService.IsUnderDirectory(dir, dir));
        Assert.IsTrue(JsonRpcExtensionService.IsUnderDirectory(dir + Path.DirectorySeparatorChar, dir));
    }

    [TestMethod]
    public void IsUnderDirectory_Descendant_IsTrue()
    {
        var dir = Path.Combine(_root, "foo");
        var file = Path.Combine(dir, "src", "index.js");
        Assert.IsTrue(JsonRpcExtensionService.IsUnderDirectory(file, dir));
    }

    [TestMethod]
    public void IsUnderDirectory_SiblingWithSharedPrefix_IsFalse()
    {
        // "foo-bar" must not be considered a child of "foo".
        var dir = Path.Combine(_root, "foo");
        var sibling = Path.Combine(_root, "foo-bar", "index.js");
        Assert.IsFalse(JsonRpcExtensionService.IsUnderDirectory(sibling, dir));
    }

    [TestMethod]
    public void IsUnderDirectory_EmptyArguments_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.IsUnderDirectory(string.Empty, _root));
        Assert.IsFalse(JsonRpcExtensionService.IsUnderDirectory(_root, string.Empty));
    }

    [TestMethod]
    public void IsExtensionPresentOnDisk_ReportsPresentDirectory()
    {
        // A crash-disabled or corrupt install leaves the directory on disk even when the provider
        // never loaded. Present-on-disk detection is what lets the gallery still offer Uninstall.
        var present = Path.Combine(_root, "present-but-unloaded");
        Directory.CreateDirectory(present);

        Assert.IsTrue(JsonRpcExtensionService.IsExtensionPresentOnDisk(present));
        Assert.IsFalse(JsonRpcExtensionService.IsExtensionPresentOnDisk(Path.Combine(_root, "missing")));
        Assert.IsFalse(JsonRpcExtensionService.IsExtensionPresentOnDisk(string.Empty));
    }

    private void CreateExtension(string dirName, string packageJson, string? entryPointRelativePath)
    {
        var dir = Path.Combine(_root, dirName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "package.json"), packageJson);

        if (entryPointRelativePath is not null)
        {
            var entryPath = Path.Combine(dir, entryPointRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
            File.WriteAllText(entryPath, "// entry");
        }
    }
}
