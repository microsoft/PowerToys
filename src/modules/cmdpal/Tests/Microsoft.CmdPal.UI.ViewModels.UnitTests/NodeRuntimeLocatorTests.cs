// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NodeRuntimeLocatorTests
{
    private static readonly string[] MalformedPathEntries = { "invalid|path" };

    [TestMethod]
    public void ResolveNodeExecutable_ReturnsFirstDirectoryThatContainsNodeExe()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmdpal-node-locator-" + Guid.NewGuid().ToString("N"));
        var withoutNode = Path.Combine(root, "without");
        var withNode = Path.Combine(root, "with");
        Directory.CreateDirectory(withoutNode);
        Directory.CreateDirectory(withNode);

        var expected = Path.Combine(withNode, "node.exe");

        try
        {
            File.WriteAllText(expected, string.Empty);

            // The first directory has no node.exe, so resolution must skip it and return
            // the absolute path from the second directory rather than the bare name.
            var resolved = NodeRuntimeLocator.ResolveNodeExecutable(new[] { withoutNode, withNode });

            Assert.AreEqual(expected, resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveNodeExecutable_ReturnsNullWhenNodeExeIsNotPresent()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmdpal-node-locator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var resolved = NodeRuntimeLocator.ResolveNodeExecutable(new[] { root });
            Assert.IsNull(resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveNodeExecutable_SkipsMalformedPathEntries()
    {
        // A PATH entry containing invalid path characters must be skipped rather than
        // throwing, so a single bad entry cannot break node.exe resolution.
        var resolved = NodeRuntimeLocator.ResolveNodeExecutable(MalformedPathEntries);
        Assert.IsNull(resolved);
    }

    [TestMethod]
    public void ResolveNodeExecutable_ReturnsNullForEmptyDirectoryList()
    {
        Assert.IsNull(NodeRuntimeLocator.ResolveNodeExecutable(Array.Empty<string>()));
    }

    [TestMethod]
    [DoNotParallelize]
    public void ResolveNodeExecutable_RejectsCurrentAndRelativeDirectories()
    {
        var originalDirectory = Environment.CurrentDirectory;
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDirectory.FullName, "node.exe"), string.Empty);
            Environment.CurrentDirectory = tempDirectory.FullName;

            var result = NodeRuntimeLocator.ResolveNodeExecutable([".", tempDirectory.Name]);

            Assert.IsNull(result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            tempDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ResolveNodeExecutable_RejectsQuotedAndMalformedDirectories()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDirectory.FullName, "node.exe"), string.Empty);

            var result = NodeRuntimeLocator.ResolveNodeExecutable(
            [
                $"\"{tempDirectory.FullName}\"",
                "\0invalid",
            ]);

            Assert.IsNull(result);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ResolveNodeExecutable_ReturnsCanonicalAbsolutePath()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var nestedDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "nested"));
            var nodePath = Path.Combine(tempDirectory.FullName, "node.exe");
            File.WriteAllText(nodePath, string.Empty);

            var result = NodeRuntimeLocator.ResolveNodeExecutable(
                [Path.Combine(nestedDirectory.FullName, "..")]);

            Assert.IsNotNull(result);
            Assert.AreEqual(Path.GetFullPath(nodePath), result);
            Assert.IsTrue(Path.IsPathFullyQualified(result));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
