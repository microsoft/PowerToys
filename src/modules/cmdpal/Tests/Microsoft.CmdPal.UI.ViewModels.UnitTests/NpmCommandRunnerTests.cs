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

    [TestMethod]
    public void ResolveNpmInvocation_UsesNodeExeAndNpmCliJs_NotNpmCmd()
    {
        // Lay out a fake Node.js install: node.exe on PATH with npm-cli.js under its node_modules.
        var nodeDir = CreateTempDirectory();
        var nodeExe = Path.Combine(nodeDir, "node.exe");
        File.WriteAllText(nodeExe, "binary");

        // A sibling npm.cmd must be ignored in favor of the JavaScript entry point.
        File.WriteAllText(Path.Combine(nodeDir, "npm.cmd"), "@echo off");

        var npmCli = Path.Combine(nodeDir, "node_modules", "npm", "bin", "npm-cli.js");
        Directory.CreateDirectory(Path.GetDirectoryName(npmCli)!);
        File.WriteAllText(npmCli, "// npm");

        var invocation = NpmCommandRunner.ResolveNpmInvocation(new[] { nodeDir });

        Assert.IsNotNull(invocation);
        Assert.AreEqual(nodeExe, invocation.Value.FileName);
        Assert.AreEqual(1, invocation.Value.LauncherArguments.Count);
        Assert.AreEqual(npmCli, invocation.Value.LauncherArguments[0]);
        Assert.IsFalse(invocation.Value.FileName.EndsWith("npm.cmd", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResolveNpmInvocation_ReturnsNull_WhenNpmCliMissing()
    {
        var nodeDir = CreateTempDirectory();
        File.WriteAllText(Path.Combine(nodeDir, "node.exe"), "binary");

        // No npm-cli.js anywhere reachable from this directory.
        var invocation = NpmCommandRunner.ResolveNpmInvocation(new[] { nodeDir });

        Assert.IsNull(invocation);
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_AcceptsRegistrySourcedTreeWithIntegrity()
    {
        var dir = CreateTempDirectory();
        var lockfile = """
        {
          "lockfileVersion": 3,
          "packages": {
            "": { "name": "root" },
            "node_modules/left-pad": {
              "resolved": "https://registry.npmjs.org/left-pad/-/left-pad-1.3.0.tgz",
              "integrity": "sha512-abc123=="
            }
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), lockfile);

        Assert.IsNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_RejectsNonRegistryResolution()
    {
        var dir = CreateTempDirectory();
        var lockfile = """
        {
          "lockfileVersion": 3,
          "packages": {
            "": { "name": "root" },
            "node_modules/evil": {
              "resolved": "https://evil.example.com/evil/-/evil-1.0.0.tgz",
              "integrity": "sha512-abc123=="
            }
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), lockfile);

        Assert.IsNotNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_RejectsIntegrityLessResolution()
    {
        var dir = CreateTempDirectory();
        var lockfile = """
        {
          "lockfileVersion": 3,
          "packages": {
            "": { "name": "root" },
            "node_modules/left-pad": {
              "resolved": "https://registry.npmjs.org/left-pad/-/left-pad-1.3.0.tgz"
            }
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), lockfile);

        Assert.IsNotNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_FailsClosed_WhenLockfileMissing()
    {
        var dir = CreateTempDirectory();

        Assert.IsNotNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_RejectsLegacyFileResolution()
    {
        var dir = CreateTempDirectory();
        var lockfile = """
        {
          "lockfileVersion": 1,
          "dependencies": {
            "left-pad": {
              "version": "file:../left-pad",
              "resolved": "file:../left-pad"
            }
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), lockfile);

        Assert.IsNotNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
    }

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
