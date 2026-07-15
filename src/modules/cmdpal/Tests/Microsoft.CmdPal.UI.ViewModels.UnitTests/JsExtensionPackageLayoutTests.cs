// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class JsExtensionPackageLayoutTests
{
    private const string ScopedManifest = """
        {
          "name": "@contoso/sample",
          "version": "1.2.3",
          "main": "dist/extension.js",
          "cmdpal": { "displayName": "Contoso Sample" }
        }
        """;

    private const string NestedManifest = """
        {
          "name": "sample-ext",
          "version": "1.0.0",
          "main": "dist/extension.js",
          "cmdpal": {}
        }
        """;

    private const string RootManifest = """
        {
          "name": "already",
          "version": "1.0.0",
          "main": "dist/extension.js",
          "cmdpal": {}
        }
        """;

    private string _root = string.Empty;

    [TestInitialize]
    public void TestInitialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "cmdpal-layout-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void Materialize_HoistsScopedPackage_AndDiscoveryFindsIt()
    {
        var extensionDir = Path.Combine(_root, "contoso-sample");
        var packageDir = Path.Combine(extensionDir, "node_modules", "@contoso", "sample");

        // The manifest npm writes at the install-target root does not carry a cmdpal section.
        Directory.CreateDirectory(extensionDir);
        File.WriteAllText(
            Path.Combine(extensionDir, "package.json"),
            """{ "dependencies": { "@contoso/sample": "1.2.3" } }""");

        // The real extension lands under node_modules/<package>.
        Directory.CreateDirectory(Path.Combine(packageDir, "dist"));
        File.WriteAllText(Path.Combine(packageDir, "package.json"), ScopedManifest);
        File.WriteAllText(Path.Combine(packageDir, "dist", "extension.js"), "// entry");

        // A hoisted runtime dependency sits directly under the top node_modules.
        var depDir = Path.Combine(extensionDir, "node_modules", "left-pad");
        Directory.CreateDirectory(depDir);
        File.WriteAllText(Path.Combine(depDir, "index.js"), "module.exports = {};");

        var result = JsExtensionPackageLayout.Materialize(extensionDir);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        Assert.IsTrue(File.Exists(Path.Combine(extensionDir, "package.json")));
        Assert.IsTrue(File.Exists(Path.Combine(extensionDir, "dist", "extension.js")));
        Assert.IsTrue(Directory.Exists(depDir), "The hoisted dependency should be preserved.");
        Assert.IsFalse(Directory.Exists(Path.Combine(extensionDir, "node_modules", "@contoso")), "The scoped package folder should be removed after hoisting.");

        var discovered = JsonRpcExtensionService.DiscoverManifests(_root);
        Assert.AreEqual(1, discovered.Count);
        Assert.AreEqual("@contoso/sample", discovered[0].Manifest.Name);
        Assert.AreEqual("Contoso Sample", discovered[0].Manifest.DisplayName);
    }

    [TestMethod]
    public void Materialize_MergesNestedNodeModules_IntoTopLevel()
    {
        var extensionDir = Path.Combine(_root, "nested-sample");
        var packageDir = Path.Combine(extensionDir, "node_modules", "sample-ext");

        Directory.CreateDirectory(Path.Combine(packageDir, "dist"));
        File.WriteAllText(Path.Combine(packageDir, "package.json"), NestedManifest);
        File.WriteAllText(Path.Combine(packageDir, "dist", "extension.js"), "// entry");

        // A version-pinned dependency nested inside the package's own node_modules.
        var nestedDep = Path.Combine(packageDir, "node_modules", "nested-dep");
        Directory.CreateDirectory(nestedDep);
        File.WriteAllText(Path.Combine(nestedDep, "index.js"), "module.exports = 1;");

        // A hoisted dependency already at the top node_modules.
        var topDep = Path.Combine(extensionDir, "node_modules", "top-dep");
        Directory.CreateDirectory(topDep);
        File.WriteAllText(Path.Combine(topDep, "index.js"), "module.exports = 2;");

        var result = JsExtensionPackageLayout.Materialize(extensionDir);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        Assert.IsTrue(Directory.Exists(Path.Combine(extensionDir, "node_modules", "top-dep")));
        Assert.IsTrue(Directory.Exists(Path.Combine(extensionDir, "node_modules", "nested-dep")), "The nested dependency should be merged into the top node_modules.");
        Assert.IsFalse(Directory.Exists(Path.Combine(extensionDir, "node_modules", "sample-ext")));
    }

    [TestMethod]
    public void Materialize_IsNoOp_WhenRootAlreadyHasManifest()
    {
        var extensionDir = Path.Combine(_root, "already-materialized");
        Directory.CreateDirectory(Path.Combine(extensionDir, "dist"));
        File.WriteAllText(Path.Combine(extensionDir, "package.json"), RootManifest);
        File.WriteAllText(Path.Combine(extensionDir, "dist", "extension.js"), "// entry");

        var result = JsExtensionPackageLayout.Materialize(extensionDir);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        var discovered = JsonRpcExtensionService.DiscoverManifests(_root);
        Assert.AreEqual(1, discovered.Count);
        Assert.AreEqual("already", discovered[0].Manifest.Name);
    }

    [TestMethod]
    public void Materialize_Fails_WhenInstalledPackageIsNotACmdPalExtension()
    {
        var extensionDir = Path.Combine(_root, "not-cmdpal");
        var packageDir = Path.Combine(extensionDir, "node_modules", "plain-lib");
        Directory.CreateDirectory(packageDir);

        // No cmdpal section anywhere.
        File.WriteAllText(Path.Combine(extensionDir, "package.json"), """{ "dependencies": {} }""");
        File.WriteAllText(
            Path.Combine(packageDir, "package.json"),
            """{ "name": "plain-lib", "version": "1.0.0", "main": "index.js" }""");
        File.WriteAllText(Path.Combine(packageDir, "index.js"), "module.exports = {};");

        var result = JsExtensionPackageLayout.Materialize(extensionDir);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(JsonRpcExtensionService.DiscoverManifests(_root).Any());
    }
}
