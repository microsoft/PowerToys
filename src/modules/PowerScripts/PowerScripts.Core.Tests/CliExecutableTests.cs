// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliExecutableTests
{
    [DataTestMethod]
    [DataRow("tool.exe")]
    [DataRow("native/tool.exe")]
    [DataRow(@"native\tool.exe")]
    public void RelativeExecutable_AllowsNativeExecutablesWithinTheDeployment(string relativePath)
    {
        Assert.IsTrue(CliExecutableResolver.IsValidRelativeExecutable(relativePath));
    }

    [DataTestMethod]
    [DataRow("../tool.exe")]
    [DataRow(@"native\..\tool.exe")]
    [DataRow(@"C:\Windows\cmd.exe")]
    [DataRow(@"C:tool.exe")]
    [DataRow(@"\\server\share\tool.exe")]
    [DataRow("/tool.exe")]
    [DataRow("tool.exe:stream")]
    [DataRow("tool.cmd")]
    [DataRow("native//tool.exe")]
    [DataRow("native./tool.exe")]
    [DataRow("native /tool.exe")]
    public void RelativeExecutable_RejectsExternalTraversalAndAmbiguousPaths(string relativePath)
    {
        Assert.IsFalse(CliExecutableResolver.IsValidRelativeExecutable(relativePath));
    }

    [TestMethod]
    public void Availability_DoesNotSearchPathForAMissingInstalledExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "PowerScripts-CliPath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var availability = new CliExecutor(root).CheckAvailability(new BuiltinCliDefinition { Executable = "cmd.exe" });

            Assert.IsFalse(availability.IsAvailable);
            Assert.IsNull(availability.ExecutablePath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(availability.UnavailableReason));
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    [TestMethod]
    public void Availability_RejectsAnExecutableBelowASymbolicLink()
    {
        var root = Path.Combine(Path.GetTempPath(), "PowerScripts-CliLink-" + Guid.NewGuid().ToString("N"));
        var installation = Path.Combine(root, "install");
        var outside = Path.Combine(root, "outside");
        var link = Path.Combine(installation, "linked");
        Directory.CreateDirectory(installation);
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "tool.exe"), [0x4d, 0x5a]);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("Creating a symbolic link is unavailable on this test host: " + ex.Message);
            }

            var availability = new CliExecutor(installation).CheckAvailability(new BuiltinCliDefinition { Executable = "linked/tool.exe" });

            Assert.IsFalse(availability.IsAvailable);
            StringAssert.Contains(availability.UnavailableReason, "symbolic links");
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(root, recursive: true);
        }
    }
}
