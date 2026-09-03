// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NpmCommandRunnerTests
{
    private const string ValidIntegrity = "sha512-abc123==";

    [TestMethod]
    public void BuildPackArguments_UsesExactSpec_DisablesScripts_AndWritesToDestination()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("@contoso/sample", "1.2.3", ValidIntegrity, null, out var artifact, out _));

        var args = NpmCommandRunner.BuildPackArguments(artifact!, "C:\\stage\\pack").ToArray();

        Assert.AreEqual("pack", args[0]);
        Assert.AreEqual("@contoso/sample@1.2.3", args[1]);
        CollectionAssert.Contains(args, "--ignore-scripts");
        CollectionAssert.Contains(args, "--no-audit");
        CollectionAssert.Contains(args, "--no-fund");
        CollectionAssert.Contains(args, "--loglevel=error");

        var destinationIndex = args.ToList().IndexOf("--pack-destination");
        Assert.IsTrue(destinationIndex >= 0);
        Assert.AreEqual("C:\\stage\\pack", args[destinationIndex + 1]);

        // With no registry configured, the registry flag is never emitted.
        CollectionAssert.DoesNotContain(args, "--registry");
    }

    [TestMethod]
    public void BuildPackArguments_NeverEmitsAFlagLikeSpec()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, null, out var artifact, out _));

        var args = NpmCommandRunner.BuildPackArguments(artifact!, "C:\\stage\\pack").ToArray();

        // The spec token must not begin with '-', or npm could read it as a flag.
        Assert.IsFalse(args[1].StartsWith('-'));
    }

    [TestMethod]
    public void BuildPackArguments_PassesApprovedRegistry_ThroughItsOwnFlag()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, "https://registry.npmjs.org/", out var artifact, out _));

        var args = NpmCommandRunner.BuildPackArguments(artifact!, "C:\\stage\\pack").ToArray();

        var registryIndex = args.ToList().IndexOf("--registry");
        Assert.IsTrue(registryIndex >= 0);
        Assert.AreEqual("https://registry.npmjs.org/", args[registryIndex + 1]);
    }

    [TestMethod]
    public void BuildCiArguments_RunsCi_WithoutAnyPackageSpec()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("@contoso/sample", "1.2.3", ValidIntegrity, null, out var artifact, out _));

        var args = NpmCommandRunner.BuildCiArguments(artifact!).ToArray();

        Assert.AreEqual("ci", args[0]);
        CollectionAssert.Contains(args, "--ignore-scripts");
        CollectionAssert.Contains(args, "--no-audit");
        CollectionAssert.Contains(args, "--no-fund");
        CollectionAssert.Contains(args, "--loglevel=error");

        // npm ci installs the frozen closure named in the shrinkwrap. Do not pass a package spec or
        // version, because that would open range resolution again.
        CollectionAssert.DoesNotContain(args, artifact!.InstallSpec);
        CollectionAssert.DoesNotContain(args, "install");
        CollectionAssert.DoesNotContain(args, "--save-exact");
    }

    [TestMethod]
    public void BuildCiArguments_PassesApprovedRegistry_ThroughItsOwnFlag()
    {
        Assert.IsTrue(NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, "https://registry.npmjs.org/", out var artifact, out _));

        var args = NpmCommandRunner.BuildCiArguments(artifact!).ToArray();

        var registryIndex = args.ToList().IndexOf("--registry");
        Assert.IsTrue(registryIndex >= 0);
        Assert.AreEqual("https://registry.npmjs.org/", args[registryIndex + 1]);
    }

    [TestMethod]
    public void RequirePublisherShrinkwrap_ReturnsNull_WhenShrinkwrapPresent()
    {
        var packageRoot = CreateTempDirectory();
        File.WriteAllText(Path.Combine(packageRoot, "npm-shrinkwrap.json"), "{}");

        Assert.IsNull(NpmCommandRunner.RequirePublisherShrinkwrap(packageRoot));
    }

    [TestMethod]
    public void RequirePublisherShrinkwrap_ReturnsError_WhenShrinkwrapMissing()
    {
        // Fail closed when a package ships package.json but no frozen closure.
        var packageRoot = CreateTempDirectory();
        File.WriteAllText(Path.Combine(packageRoot, "package.json"), "{\"name\":\"x\",\"version\":\"1.0.0\"}");
        File.WriteAllText(Path.Combine(packageRoot, "package-lock.json"), "{}");

        var error = NpmCommandRunner.RequirePublisherShrinkwrap(packageRoot);

        Assert.IsNotNull(error);
        Assert.AreEqual(Microsoft.CmdPal.UI.ViewModels.Properties.Resources.npm_runner_shrinkwrap_required, error);
    }

    [TestMethod]
    public void ComputeTarballIntegrity_ReturnsSha512SubresourceIntegrity()
    {
        var dir = CreateTempDirectory();
        var tarball = Path.Combine(dir, "sample.tgz");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(tarball, bytes);

        var expected = "sha512-" + Convert.ToBase64String(System.Security.Cryptography.SHA512.HashData(bytes));

        Assert.AreEqual(expected, NpmCommandRunner.ComputeTarballIntegrity(tarball));
    }

    [TestMethod]
    public void FindPackedTarball_ReturnsTheSingleTgz()
    {
        var dir = CreateTempDirectory();
        var tarball = Path.Combine(dir, "left-pad-1.3.0.tgz");
        File.WriteAllText(tarball, "x");

        Assert.AreEqual(tarball, NpmCommandRunner.FindPackedTarball(dir));
    }

    [TestMethod]
    public void FindPackedTarball_ReturnsNull_WhenNoTarball()
    {
        var dir = CreateTempDirectory();

        Assert.IsNull(NpmCommandRunner.FindPackedTarball(dir));
    }

    [TestMethod]
    public async Task TerminateAndWaitAsync_AggregateFailure_PreservesCallerCancellation()
    {
        var originalCancellation = new OperationCanceledException();

        var thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            try
            {
                throw originalCancellation;
            }
            catch (OperationCanceledException)
            {
                await NpmCommandRunner.TerminateAndWaitAsync(
                    () => false,
                    () => throw new AggregateException(new Win32Exception("kill failed")),
                    _ => Task.CompletedTask);
                throw;
            }
        });

        Assert.AreSame(originalCancellation, thrown);
    }

    [TestMethod]
    public async Task TerminateAndWaitAsync_AggregateFailure_PreservesTimeoutHandling()
    {
        var timeoutHandled = false;

        try
        {
            throw new OperationCanceledException();
        }
        catch (OperationCanceledException)
        {
            await NpmCommandRunner.TerminateAndWaitAsync(
                () => false,
                () => throw new AggregateException(new NotSupportedException("kill failed")),
                _ => Task.CompletedTask);
            timeoutHandled = true;
        }

        Assert.IsTrue(timeoutHandled);
    }

    [TestMethod]
    public async Task TerminateAndWaitAsync_AggregateWithUnexpectedFailure_Propagates()
    {
        await Assert.ThrowsExactlyAsync<AggregateException>(() =>
            NpmCommandRunner.TerminateAndWaitAsync(
                () => false,
                () => throw new AggregateException(new Win32Exception(), new InvalidDataException()),
                _ => Task.CompletedTask));
    }

    [TestMethod]
    public void TryExtractPackage_ExtractsPublishedRoot_WithEmbeddedShrinkwrap()
    {
        // Build a real npm tarball (gzip tar rooted under "package/") with a shrinkwrap, then prove
        // the runner makes the published package the root project so the gate can see the frozen
        // closure. No network needed.
        var dir = CreateTempDirectory();
        var tarball = Path.Combine(dir, "sample.tgz");
        WriteNpmTarball(tarball, new[]
        {
            ("package/package.json", "{\"name\":\"sample\",\"version\":\"1.0.0\"}"),
            ("package/npm-shrinkwrap.json", "{\"lockfileVersion\":3,\"packages\":{\"\":{\"name\":\"sample\"}}}"),
            ("package/index.js", "module.exports = {};"),
        });

        var packageRoot = Path.Combine(dir, "package");

        var error = NpmCommandRunner.TryExtractPackage(tarball, packageRoot);

        Assert.IsNull(error);
        Assert.IsTrue(File.Exists(Path.Combine(packageRoot, "package.json")));
        Assert.IsTrue(File.Exists(Path.Combine(packageRoot, "npm-shrinkwrap.json")));
        Assert.IsNull(NpmCommandRunner.RequirePublisherShrinkwrap(packageRoot));
    }

    [TestMethod]
    public void TryExtractPackage_ThenRequireShrinkwrap_RejectsTarballWithoutShrinkwrap()
    {
        var dir = CreateTempDirectory();
        var tarball = Path.Combine(dir, "sample.tgz");
        WriteNpmTarball(tarball, new[]
        {
            ("package/package.json", "{\"name\":\"sample\",\"version\":\"1.0.0\"}"),
            ("package/index.js", "module.exports = {};"),
        });

        var packageRoot = Path.Combine(dir, "package");

        Assert.IsNull(NpmCommandRunner.TryExtractPackage(tarball, packageRoot));
        Assert.IsNotNull(NpmCommandRunner.RequirePublisherShrinkwrap(packageRoot));
    }

    [TestMethod]
    public void VerifyLockfileIntegrity_ReadsPublisherShrinkwrap_WhenNoPackageLock()
    {
        // Only the shrinkwrap is present, as it would be after npm ci consumes it. The trusted URL and
        // SRI gate must accept a fully pinned closure from the registry.
        var dir = CreateTempDirectory();
        var shrinkwrap = """
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
        File.WriteAllText(Path.Combine(dir, "npm-shrinkwrap.json"), shrinkwrap);

        Assert.IsNull(NpmCommandRunner.VerifyLockfileIntegrity(dir));
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

            // The runner must refuse to recurse through the reparse point, and the real target must stay
            // untouched.
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

    [TestMethod]
    public void ResolveNpmInvocation_UsesNodeExeAndNpmCliJs_NotNpmCmd()
    {
        // Lay out a fake Node.js install: node.exe on PATH with npm-cli.js under node_modules.
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

    /// <summary>
    /// Writes a gzip compressed tar archive in npm's published format so extraction can be tested
    /// without network access.
    /// </summary>
    private static void WriteNpmTarball(string tarballPath, (string Name, string Content)[] entries)
    {
        using var fileStream = File.Create(tarballPath);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
        using var tarWriter = new TarWriter(gzipStream, TarEntryFormat.Pax);

        foreach (var (name, content) in entries)
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, name);
            var bytes = Encoding.UTF8.GetBytes(content);
            entry.DataStream = new MemoryStream(bytes);
            tarWriter.WriteEntry(entry);
        }
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
