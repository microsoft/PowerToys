// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Covers the robust-discovery and duplicate-id remediations (p4-04, p4-07): the
/// reconciliation diff, the deterministic collision policy, the manifest-stability
/// retry, and mapping a changed path back to its owning extension directory.
/// </summary>
[TestClass]
public class JsonRpcExtensionServiceReconciliationTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"JSExtReconcile_{Guid.NewGuid():N}");
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
    public void ReconcileDirectories_ComputesAddsAndRemoves()
    {
        var discovered = new[]
        {
            @"C:\ext\alpha",
            @"C:\ext\beta",
            @"C:\ext\gamma",
        };

        var loaded = new[]
        {
            @"C:\ext\beta\",       // trailing separator, still the same directory
            @"C:\ext\DELTA",       // loaded but no longer on disk
        };

        var (toAdd, toRemove) = JsonRpcExtensionService.ReconcileDirectories(discovered, loaded);

        var addSet = toAdd.Select(DirectoryLifecycleGate.Canonicalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removeSet = toRemove.Select(DirectoryLifecycleGate.Canonicalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(addSet.Contains(DirectoryLifecycleGate.Canonicalize(@"C:\ext\alpha")));
        Assert.IsTrue(addSet.Contains(DirectoryLifecycleGate.Canonicalize(@"C:\ext\gamma")));
        Assert.IsFalse(addSet.Contains(DirectoryLifecycleGate.Canonicalize(@"C:\ext\beta")), "Already-loaded beta is not re-added.");
        Assert.AreEqual(2, addSet.Count);

        Assert.IsTrue(removeSet.Contains(DirectoryLifecycleGate.Canonicalize(@"C:\ext\DELTA")), "A loaded-but-deleted extension is reconciled out.");
        Assert.AreEqual(1, removeSet.Count);
    }

    [TestMethod]
    public void ResolveIdCollisions_DuplicateIds_DeterministicWinnerByPath()
    {
        // Two extensions in different directories advertise the same name key.
        CreateExtension("z-dir", "dup-ext");
        CreateExtension("a-dir", "dup-ext");
        CreateExtension("solo", "unique-ext");

        var discovered = JsonRpcExtensionService.DiscoverManifests(_root);
        var (accepted, rejected) = JsonRpcExtensionService.ResolveIdCollisions(discovered);

        // The winner is deterministic: the canonical-path-sorted first directory wins,
        // independent of enumeration order. "a-dir" sorts before "z-dir".
        var acceptedDirs = accepted.Select(a => Path.GetFileName(a.Directory)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(acceptedDirs.Contains("a-dir"), "The path-sorted first duplicate wins.");
        Assert.IsTrue(acceptedDirs.Contains("solo"), "A non-duplicate is always accepted.");
        Assert.IsFalse(acceptedDirs.Contains("z-dir"), "The losing duplicate is rejected.");

        Assert.AreEqual(1, rejected.Count);
        Assert.AreEqual("z-dir", Path.GetFileName(rejected[0].Directory));
        Assert.AreEqual(
            DirectoryLifecycleGate.Canonicalize(Path.Combine(_root, "a-dir")),
            rejected[0].WinnerDirectory,
            "The rejection records the deterministic winner directory.");
    }

    [TestMethod]
    public void ResolveIdCollisions_IsStableAcrossInputOrder()
    {
        CreateExtension("z-dir", "dup-ext");
        CreateExtension("a-dir", "dup-ext");

        var discovered = JsonRpcExtensionService.DiscoverManifests(_root).ToList();

        var forward = JsonRpcExtensionService.ResolveIdCollisions(discovered);
        discovered.Reverse();
        var reversed = JsonRpcExtensionService.ResolveIdCollisions(discovered);

        var forwardWinner = Path.GetFileName(forward.Accepted.Single().Directory);
        var reversedWinner = Path.GetFileName(reversed.Accepted.Single().Directory);

        Assert.AreEqual("a-dir", forwardWinner);
        Assert.AreEqual(forwardWinner, reversedWinner, "The winner is independent of input order.");
    }

    [TestMethod]
    public async Task WaitForStableManifestAsync_RetriesUntilManifestParses()
    {
        var attempts = 0;
        JSExtensionManifestParseResult Parse(string manifestPath)
        {
            attempts++;

            // Simulate a slow install: the first two reads see a partially written
            // package that does not parse, the third read succeeds.
            if (attempts < 3)
            {
                return JSExtensionManifestParseResult.Failure("still being written");
            }

            return JSExtensionManifestParseResult.Success(new JSExtensionManifest { Name = "slow-ext" });
        }

        var delays = 0;
        Task Delay(int attempt, CancellationToken token)
        {
            delays++;
            return Task.CompletedTask;
        }

        var manifest = await JsonRpcExtensionService.WaitForStableManifestAsync(
            "package.json",
            attempts: 5,
            Parse,
            Delay,
            CancellationToken.None);

        Assert.IsNotNull(manifest);
        Assert.AreEqual("slow-ext", manifest!.Name);
        Assert.AreEqual(3, attempts);
        Assert.AreEqual(2, delays, "It waited between the failed attempts.");
    }

    [TestMethod]
    public async Task WaitForStableManifestAsync_NeverValid_ReturnsNull()
    {
        var manifest = await JsonRpcExtensionService.WaitForStableManifestAsync(
            "package.json",
            attempts: 3,
            _ => JSExtensionManifestParseResult.Failure("bad"),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.IsNull(manifest);
    }

    [TestMethod]
    public void GetExtensionDirectoryForPath_ReturnsOwningTopLevelDirectory()
    {
        var manifestPath = Path.Combine(_root, "my-ext", "package.json");
        var sourcePath = Path.Combine(_root, "my-ext", "src", "index.js");

        var expected = Path.Combine(Path.GetFullPath(_root), "my-ext");

        Assert.AreEqual(expected, JsonRpcExtensionService.GetExtensionDirectoryForPath(_root, manifestPath));
        Assert.AreEqual(expected, JsonRpcExtensionService.GetExtensionDirectoryForPath(_root, sourcePath));
    }

    [TestMethod]
    public void GetExtensionDirectoryForPath_PathOutsideRoot_ReturnsNull()
    {
        Assert.IsNull(JsonRpcExtensionService.GetExtensionDirectoryForPath(_root, @"C:\somewhere\else\file.js"));
        Assert.IsNull(JsonRpcExtensionService.GetExtensionDirectoryForPath(_root, _root));
    }

    private void CreateExtension(string dirName, string extensionName)
    {
        var dir = Path.Combine(_root, dirName);
        Directory.CreateDirectory(dir);
        var packageJson = $$"""
        {
            "name": "{{extensionName}}",
            "main": "index.js",
            "cmdpal": { "displayName": "{{extensionName}}" }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "package.json"), packageJson);
        File.WriteAllText(Path.Combine(dir, "index.js"), "// entry");
    }
}
