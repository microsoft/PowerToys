// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the pure watcher-routing decisions used by the directory and source
/// watchers (r2-p4-04, r2-p4-05, r2-p4-06). These are the same decisions the live
/// watchers make, extracted so they can be tested without spinning up a real
/// FileSystemWatcher or a Node process:
/// <list type="bullet">
///   <item>Churn under node_modules/.git subtrees must not trigger reloads (p4-04).</item>
///   <item>Rename/delete-derived source paths still route to a reload (p4-05).</item>
///   <item>A manifest edit is detectable so an explicit refresh reloads it (p4-06).</item>
/// </list>
/// </summary>
[TestClass]
public class JsonRpcExtensionServiceWatcherRoutingTests
{
    private static string Path3(string a, string b, string c) => Path.Combine(a, b, c);

    [TestMethod]
    public void HasIgnoredDirectorySegment_NodeModules_IsTrue()
    {
        var path = Path.Combine(@"C:\ext\my-extension", "node_modules", "left-pad", "index.js");
        Assert.IsTrue(JsonRpcExtensionService.HasIgnoredDirectorySegment(path));
    }

    [TestMethod]
    public void HasIgnoredDirectorySegment_NestedNodeModules_IsTrue()
    {
        // A deeply nested node_modules tree (the restart-storm source) must still be caught.
        var path = Path.Combine(
            @"C:\ext\my-extension",
            "node_modules",
            "a",
            "node_modules",
            "b",
            "package.json");
        Assert.IsTrue(JsonRpcExtensionService.HasIgnoredDirectorySegment(path));
    }

    [TestMethod]
    public void HasIgnoredDirectorySegment_GitFolder_IsTrue()
    {
        var path = Path3(@"C:\ext\my-extension", ".git", "index");
        Assert.IsTrue(JsonRpcExtensionService.HasIgnoredDirectorySegment(path));
    }

    [TestMethod]
    public void HasIgnoredDirectorySegment_SimilarlyNamedFolder_IsFalse()
    {
        // A directory whose name merely contains "node_modules" is not the real thing.
        var path = Path3(@"C:\ext\my-extension", "node_modules_backup", "index.js");
        Assert.IsFalse(JsonRpcExtensionService.HasIgnoredDirectorySegment(path));

        var git = Path3(@"C:\ext\my-extension", "gitignore-samples", "index.js");
        Assert.IsFalse(JsonRpcExtensionService.HasIgnoredDirectorySegment(git));
    }

    [TestMethod]
    public void HasIgnoredDirectorySegment_ForwardSlashes_AreHonored()
    {
        Assert.IsTrue(JsonRpcExtensionService.HasIgnoredDirectorySegment("C:/ext/my-extension/node_modules/pkg/index.js"));
    }

    [TestMethod]
    public void HasIgnoredDirectorySegment_Empty_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.HasIgnoredDirectorySegment(string.Empty));
        Assert.IsFalse(JsonRpcExtensionService.HasIgnoredDirectorySegment(null!));
    }

    [TestMethod]
    public void ShouldReloadForSourceChange_JavaScriptSource_IsTrue()
    {
        // A plain source edit, a rename target, and a delete all arrive as full paths;
        // each must route to a reload.
        Assert.IsTrue(JsonRpcExtensionService.ShouldReloadForSourceChange(Path3(@"C:\ext\my-extension", "src", "index.js")));
        Assert.IsTrue(JsonRpcExtensionService.ShouldReloadForSourceChange(Path.Combine(@"C:\ext\my-extension", "commands.mjs")));
        Assert.IsTrue(JsonRpcExtensionService.ShouldReloadForSourceChange(Path.Combine(@"C:\ext\my-extension", "legacy.cjs")));
    }

    [TestMethod]
    public void ShouldReloadForSourceChange_UnderNodeModules_IsFalse()
    {
        // Even though it is a .js file, a change under node_modules must never reload.
        var path = Path.Combine(@"C:\ext\my-extension", "node_modules", "dep", "index.js");
        Assert.IsFalse(JsonRpcExtensionService.ShouldReloadForSourceChange(path));
    }

    [TestMethod]
    public void ShouldReloadForSourceChange_NonSourceFile_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.ShouldReloadForSourceChange(Path.Combine(@"C:\ext\my-extension", "README.md")));
        Assert.IsFalse(JsonRpcExtensionService.ShouldReloadForSourceChange(Path.Combine(@"C:\ext\my-extension", "styles.css")));
    }

    [TestMethod]
    public void ShouldReloadForSourceChange_Empty_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.ShouldReloadForSourceChange(string.Empty));
        Assert.IsFalse(JsonRpcExtensionService.ShouldReloadForSourceChange(null!));
    }

    [TestMethod]
    public void ManifestChanged_IdenticalManifests_IsFalse()
    {
        var manifest = SampleManifest();
        Assert.IsFalse(JsonRpcExtensionService.ManifestChanged(manifest, manifest with { }));
    }

    [TestMethod]
    public void ManifestChanged_DisplayNameEdited_IsTrue()
    {
        var loaded = SampleManifest();
        var current = loaded with { DisplayName = "Renamed Extension" };
        Assert.IsTrue(JsonRpcExtensionService.ManifestChanged(loaded, current));
    }

    [TestMethod]
    public void ManifestChanged_VersionEdited_IsTrue()
    {
        var loaded = SampleManifest();
        Assert.IsTrue(JsonRpcExtensionService.ManifestChanged(loaded, loaded with { Version = "2.0.0" }));
    }

    [TestMethod]
    public void ManifestChanged_EntryPointEdited_IsTrue()
    {
        var loaded = SampleManifest();
        var current = loaded with { EntryPointPath = @"C:\ext\my-extension\dist\index.js" };
        Assert.IsTrue(JsonRpcExtensionService.ManifestChanged(loaded, current));
    }

    [TestMethod]
    public void ManifestChanged_DebugToggled_IsTrue()
    {
        var loaded = SampleManifest();
        Assert.IsTrue(JsonRpcExtensionService.ManifestChanged(loaded, loaded with { Debug = true }));
        Assert.IsTrue(JsonRpcExtensionService.ManifestChanged(loaded, loaded with { DebugPort = 9333 }));
    }

    [TestMethod]
    public void ManifestChanged_NullOperand_IsFalse()
    {
        var loaded = SampleManifest();
        Assert.IsFalse(JsonRpcExtensionService.ManifestChanged(null!, loaded));
        Assert.IsFalse(JsonRpcExtensionService.ManifestChanged(loaded, null!));
    }

    // r3-p4-02: the recursive root watcher reports every descendant path. Only a top-level
    // <root>/<extdir> directory or its own <root>/<extdir>/package.json manifest is an
    // extension entry; anything deeper (a nested package or a node_modules manifest) must be
    // ignored so a nested package.json is not treated as an extension upsert.
    [TestMethod]
    public void IsTopLevelExtensionChange_ExtensionDirectory_IsTrue()
    {
        Assert.IsTrue(JsonRpcExtensionService.IsTopLevelExtensionChange(@"C:\root", @"C:\root\my-extension"));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_TopLevelManifest_IsTrue()
    {
        Assert.IsTrue(JsonRpcExtensionService.IsTopLevelExtensionChange(@"C:\root", @"C:\root\my-extension\package.json"));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_NestedManifest_IsFalse()
    {
        // A package.json inside a nested package or under node_modules is two-plus levels
        // below the extension directory and is not an extension entry.
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(
            @"C:\root", Path.Combine(@"C:\root", "my-extension", "node_modules", "dep", "package.json")));

        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(
            @"C:\root", Path.Combine(@"C:\root", "my-extension", "packages", "inner", "package.json")));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_NestedDirectory_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(
            @"C:\root", Path.Combine(@"C:\root", "my-extension", "dist")));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_NonManifestTopLevelFile_IsFalse()
    {
        // A file that sits at <root>/<extdir>/<file> but is not the manifest is not an entry.
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(
            @"C:\root", Path.Combine(@"C:\root", "my-extension", "index.js")));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_OutsideRoot_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(@"C:\root", @"C:\other\my-extension\package.json"));
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(@"C:\root", @"C:\root"));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_ForwardSlashesAndCasing_AreHonored()
    {
        Assert.IsTrue(JsonRpcExtensionService.IsTopLevelExtensionChange("C:/root", "C:/root/my-extension/Package.json"));
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange("C:/root", "C:/root/my-extension/node_modules/dep/package.json"));
    }

    [TestMethod]
    public void IsTopLevelExtensionChange_Empty_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(string.Empty, @"C:\root\ext"));
        Assert.IsFalse(JsonRpcExtensionService.IsTopLevelExtensionChange(@"C:\root", string.Empty));
    }

    private static JSExtensionManifest SampleManifest() => new()
    {
        Name = "my-extension",
        DisplayName = "My Extension",
        Version = "1.0.0",
        Description = "A sample extension.",
        Icon = "\uE700",
        Publisher = "Contoso",
        Main = "index.js",
        EntryPointPath = @"C:\ext\my-extension\index.js",
        Debug = false,
        DebugPort = null,
    };
}
