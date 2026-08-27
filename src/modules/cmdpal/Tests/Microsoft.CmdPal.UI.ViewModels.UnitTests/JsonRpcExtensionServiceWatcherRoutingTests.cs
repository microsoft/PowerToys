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
///   <item>Churn under node_modules must not trigger reloads (p4-04); other directories
///   (for example .git) are kept out of scope by the manifest-driven watch root instead
///   of a hardcoded ignore list.</item>
///   <item>Rename/delete-derived source paths still route to a reload (p4-05).</item>
///   <item>A manifest edit is detectable so an explicit refresh reloads it (p4-06).</item>
///   <item>A per-extension source watcher's watch root is resolved from the manifest
///   (p5-01), and a watch root that changes while the watcher is live is recognized as
///   needing repair rather than silently continuing to watch a stale root (p5-02).</item>
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
    public void HasIgnoredDirectorySegment_GitFolder_IsFalse()
    {
        // Unlike node_modules, .git is not a host-guessed name on the ignore list: the
        // per-extension source watcher's scope is manifest-driven (ResolveWatchRoot), so a
        // repository's VCS metadata is kept out of scope by not being watched in the first
        // place rather than by the host maintaining a blocklist of directory names.
        var path = Path3(@"C:\ext\my-extension", ".git", "index");
        Assert.IsFalse(JsonRpcExtensionService.HasIgnoredDirectorySegment(path));
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
    public void ShouldReloadForSourceChange_UnderGit_IsTrueWhenSourceExtension()
    {
        // .git is no longer a host-guessed ignore segment. A .js file that happens to sit
        // under .git (for example a hook or a vendored dependency) is not specially
        // exempted; keeping it out of the watch is now the manifest's job (a narrower
        // cmdpal.watchPath or entry-point-directory default), not a hardcoded directory name.
        var path = Path3(@"C:\ext\my-extension", ".git", "hook.js");
        Assert.IsTrue(JsonRpcExtensionService.ShouldReloadForSourceChange(path));
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
    public void ManifestChanged_WatchDirectoryEdited_IsTrue()
    {
        var loaded = SampleManifest();
        var current = loaded with { WatchDirectory = @"C:\ext\my-extension\src" };
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

    [TestMethod]
    public void ShouldRouteDirectoryRemoval_TopLevelDirectoryOrManifest_IsTrue()
    {
        Assert.IsTrue(JsonRpcExtensionService.ShouldRouteDirectoryRemoval(@"C:\root", @"C:\root\my-extension"));
        Assert.IsTrue(JsonRpcExtensionService.ShouldRouteDirectoryRemoval(
            @"C:\root", @"C:\root\my-extension\package.json"));
    }

    [TestMethod]
    public void ShouldRouteDirectoryRemoval_NestedPath_IsFalse()
    {
        Assert.IsFalse(JsonRpcExtensionService.ShouldRouteDirectoryRemoval(
            @"C:\root", @"C:\root\my-extension\dist\generated.js"));
        Assert.IsFalse(JsonRpcExtensionService.ShouldRouteDirectoryRemoval(
            @"C:\root", @"C:\root\my-extension\src"));
    }

    [TestMethod]
    public void ResolveWatchRoot_NoWatchDirectory_DefaultsToEntryPointDirectory()
    {
        // With no cmdpal.watchPath, the watch root narrows to the entry point's own
        // directory instead of the whole extension directory, so the host is not guessing
        // at unrelated subfolders (VCS metadata, docs, and so on) to stay out of.
        var manifest = SampleManifest();
        var root = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", manifest);
        Assert.AreEqual(@"C:\ext\my-extension", root);
    }

    [TestMethod]
    public void ResolveWatchRoot_NoWatchDirectory_NarrowsToEntryPointSubdirectory()
    {
        // An entry point that lives in a subdirectory (for example a bundler's dist/
        // output) narrows the default watch root to that subdirectory rather than the
        // whole package.
        var manifest = SampleManifest() with { EntryPointPath = @"C:\ext\my-extension\dist\index.js" };
        var root = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", manifest);
        Assert.AreEqual(@"C:\ext\my-extension\dist", root);
    }

    [TestMethod]
    public void ResolveWatchRoot_WatchDirectorySet_OverridesEntryPointDefault()
    {
        // An explicit cmdpal.watchPath wins over the entry-point-directory default,
        // letting an extension widen (or otherwise choose) its own hot-reload scope.
        var manifest = SampleManifest() with { WatchDirectory = @"C:\ext\my-extension\src" };
        var root = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", manifest);
        Assert.AreEqual(@"C:\ext\my-extension\src", root);
    }

    [TestMethod]
    public void ResolveWatchRoot_NoEntryPointOrWatchDirectory_FallsBackToExtensionDirectory()
    {
        // Defensive fallback: if neither is available (which the manifest parser does not
        // otherwise allow), the extension directory itself is used rather than throwing.
        var manifest = SampleManifest() with { EntryPointPath = null };
        var root = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", manifest);
        Assert.AreEqual(@"C:\ext\my-extension", root);
    }

    // p5-02: EnsureSourceFileWatcher (initial registration and hot-reload both call it) must
    // stay idempotent when the watch root has not moved, and repair (recreate) the watcher
    // when it has. SourceWatcherNeedsRepair is the pure decision behind that branch.
    [TestMethod]
    public void SourceWatcherNeedsRepair_SameWatchRoot_IsFalse()
    {
        // The common case: a hot-reload for an unrelated source edit resolves the same watch
        // root the live watcher already covers, so the ensure call must be a no-op rather
        // than tearing down and recreating a perfectly good watcher on every reload.
        var root = @"C:\ext\my-extension";
        Assert.IsFalse(JsonRpcExtensionService.SourceWatcherNeedsRepair(root, root));
    }

    [TestMethod]
    public void SourceWatcherNeedsRepair_TrailingSeparatorOrCasingOnly_IsFalse()
    {
        // A cosmetic difference (trailing separator, casing) must not be mistaken for a
        // real watch-root change and trigger an unnecessary watcher recreation.
        Assert.IsFalse(JsonRpcExtensionService.SourceWatcherNeedsRepair(
            @"C:\ext\my-extension\src", @"C:\ext\my-extension\src\"));
        Assert.IsFalse(JsonRpcExtensionService.SourceWatcherNeedsRepair(
            @"C:\ext\my-extension\SRC", @"C:\ext\my-extension\src"));
    }

    [TestMethod]
    public void SourceWatcherNeedsRepair_WatchPathAddedWhileRunning_IsTrue()
    {
        // This is the fix for the known limitation: a manifest reloaded via hot-reload with
        // a newly added cmdpal.watchPath must recreate the watcher at the new (narrower or
        // relocated) root instead of leaving the original one live and silently missing
        // edits under the declared watchPath.
        var previousRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", SampleManifest());
        var currentManifest = SampleManifest() with { WatchDirectory = @"C:\ext\my-extension\src" };
        var currentRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", currentManifest);

        Assert.IsTrue(JsonRpcExtensionService.SourceWatcherNeedsRepair(previousRoot, currentRoot));
    }

    [TestMethod]
    public void SourceWatcherNeedsRepair_WatchPathEditedWhileRunning_IsTrue()
    {
        // watchPath changing from one declared directory to another (not just added or
        // removed) must also be recognized as a repair, not just the added/removed cases.
        var previousManifest = SampleManifest() with { WatchDirectory = @"C:\ext\my-extension\src" };
        var previousRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", previousManifest);
        var currentManifest = SampleManifest() with { WatchDirectory = @"C:\ext\my-extension\lib" };
        var currentRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", currentManifest);

        Assert.IsTrue(JsonRpcExtensionService.SourceWatcherNeedsRepair(previousRoot, currentRoot));
    }

    [TestMethod]
    public void SourceWatcherNeedsRepair_WatchPathRemovedWhileRunning_IsTrue()
    {
        // Removing a previously declared watchPath falls back to the entry-point-directory
        // default, which is a different (wider or relocated) root and must also repair.
        var previousManifest = SampleManifest() with { WatchDirectory = @"C:\ext\my-extension\src" };
        var previousRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", previousManifest);
        var currentRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", SampleManifest());

        Assert.IsTrue(JsonRpcExtensionService.SourceWatcherNeedsRepair(previousRoot, currentRoot));
    }

    [TestMethod]
    public void SourceWatcherNeedsRepair_EntryPointDirectoryUnchanged_IsFalse()
    {
        // An entry point edit that stays within the same directory (for example a version
        // bump that does not relocate the file) must not be mistaken for a watch-root move.
        var previousRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", SampleManifest());
        var currentManifest = SampleManifest() with { EntryPointPath = @"C:\ext\my-extension\index.mjs" };
        var currentRoot = JsonRpcExtensionService.ResolveWatchRoot(@"C:\ext\my-extension", currentManifest);

        Assert.IsFalse(JsonRpcExtensionService.SourceWatcherNeedsRepair(previousRoot, currentRoot));
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
