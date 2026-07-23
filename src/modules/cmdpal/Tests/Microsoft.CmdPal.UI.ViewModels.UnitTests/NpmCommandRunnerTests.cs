// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NpmCommandRunnerTests
{
    private const string ValidIntegrity = "sha512-abc123==";

    [TestMethod]
    public void BuildInstallArguments_UsesExactSpec_AndDisablesLifecycleScripts()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("@contoso/sample", "1.2.3", ValidIntegrity, null, out var artifact, out _));

        var args = NpmCommandRunnerArguments(artifact!);

        Assert.AreEqual("install", args[0]);
        Assert.AreEqual("@contoso/sample@1.2.3", args[1]);
        CollectionAssert.Contains(args, "--ignore-scripts");
        CollectionAssert.Contains(args, "--save-exact");
        CollectionAssert.Contains(args, "--no-audit");
        CollectionAssert.Contains(args, "--no-fund");
        CollectionAssert.Contains(args, "--loglevel=error");

        // With no registry configured, the registry flag is never emitted.
        CollectionAssert.DoesNotContain(args, "--registry");
    }

    [TestMethod]
    public void BuildInstallArguments_NeverEmitsAFlagLikeSpec()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, null, out var artifact, out _));

        var args = NpmCommandRunnerArguments(artifact!);

        // The spec token (second argument) must never begin with '-', so npm cannot read it as a flag.
        Assert.IsFalse(args[1].StartsWith('-'));
    }

    [TestMethod]
    public void BuildInstallArguments_PassesApprovedRegistry_ThroughItsOwnFlag()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, "https://registry.npmjs.org/", out var artifact, out _));

        var args = NpmCommandRunnerArguments(artifact!);

        var registryIndex = args.ToList().IndexOf("--registry");
        Assert.IsTrue(registryIndex >= 0);
        Assert.AreEqual("https://registry.npmjs.org/", args[registryIndex + 1]);
    }

    [TestMethod]
    public void RemoveDirectory_DeletesAnOrdinaryDirectory()
    {
        var runner = new NpmCommandRunner();
        var dir = CreateTempDirectory();
        File.WriteAllText(Path.Combine(dir, "file.txt"), "x");

        var removed = runner.RemoveDirectory(dir);

        Assert.IsTrue(removed);
        Assert.IsFalse(Directory.Exists(dir));
    }

    [TestMethod]
    public void RemoveDirectory_ReturnsTrue_ForMissingDirectory()
    {
        var runner = new NpmCommandRunner();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.IsTrue(runner.RemoveDirectory(dir));
    }

    [TestMethod]
    public void RemoveDirectory_RefusesToFollowAJunction_AndPreservesTheTarget()
    {
        var runner = new NpmCommandRunner();
        var realTarget = CreateTempDirectory();
        var sentinel = Path.Combine(realTarget, "keep.txt");
        File.WriteAllText(sentinel, "precious");

        var junctionParent = CreateTempDirectory();
        var junction = Path.Combine(junctionParent, "link");

        if (!TryCreateJunction(junction, realTarget))
        {
            Assert.Inconclusive("Could not create a junction on this machine.");
            return;
        }

        try
        {
            var removed = runner.RemoveDirectory(junction);

            // The runner must refuse to recurse through the reparse point, and the real target's
            // contents must be untouched.
            Assert.IsFalse(removed);
            Assert.IsTrue(File.Exists(sentinel));
        }
        finally
        {
            // Remove the junction itself without following it, then the real target.
            try
            {
                Directory.Delete(junction);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string[] NpmCommandRunnerArguments(NpmArtifact artifact) =>
        NpmCommandRunner.BuildInstallArguments(artifact).ToArray();

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cmdpal-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("mklink");
            psi.ArgumentList.Add("/J");
            psi.ArgumentList.Add(junctionPath);
            psi.ArgumentList.Add(targetPath);

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(junctionPath);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
