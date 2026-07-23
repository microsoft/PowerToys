// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NpmJsExtensionInstallerTests
{
    private const string ValidIntegrity = "sha512-abc123==";
    private const string Package = "@contoso/sample";
    private const string Version = "1.2.3";
    private const string ExtensionName = "sample-ext";

    private static readonly string[] StopThenRemove = ["stop", "remove"];

    private readonly List<string> _tempRoots = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in _tempRoots)
        {
            TryDeleteTree(root);
        }
    }

    [TestMethod]
    public async Task InstallAsync_PromotesUnscopedPackage_AndReportsInstalled()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ManifestName = "left-pad", ManifestVersion = "1.3.0" };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync("left-pad-ext", "left-pad", "1.3.0", ValidIntegrity, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        var target = Path.Combine(host.ExtensionsRootPath, "left-pad-ext");
        Assert.IsTrue(Directory.Exists(target));
        Assert.IsTrue(File.Exists(Path.Combine(target, "package.json")));
        Assert.IsTrue(File.Exists(Path.Combine(target, "index.js")));
        Assert.IsTrue(host.IsExtensionInstalled("left-pad-ext"));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_PromotesScopedPackage_WithHoistedDependencies()
    {
        var host = CreateHost();
        var runner = new FakeRunner { HoistedDependencies = ["left-pad", "ms"] };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        var target = Path.Combine(host.ExtensionsRootPath, ExtensionName);
        Assert.IsTrue(File.Exists(Path.Combine(target, "package.json")));

        // The hoisted dependencies must sit under the promoted package's own node_modules.
        Assert.IsTrue(Directory.Exists(Path.Combine(target, "node_modules", "left-pad")));
        Assert.IsTrue(Directory.Exists(Path.Combine(target, "node_modules", "ms")));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_StagesOutsideTheWatchedRoot()
    {
        var host = CreateHost();
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.AreEqual(1, runner.StagingDirectories.Count);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(host.ExtensionsRootPath));
        var staging = Path.GetFullPath(runner.StagingDirectories[0]);
        Assert.IsFalse(
            staging.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            $"Staging '{staging}' must not be under the watched root '{normalizedRoot}'.");
    }

    [TestMethod]
    public async Task InstallAsync_FailsClosed_WhenArtifactIsIncomplete()
    {
        var host = CreateHost();
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        // Missing version and integrity: never installable, npm is never invoked.
        var result = await installer.InstallAsync(ExtensionName, Package, null, null, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual(0, runner.InstallCallCount);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
    }

    [TestMethod]
    public async Task InstallAsync_Fails_WhenNpmNotAvailable()
    {
        var host = CreateHost();
        var runner = new FakeRunner { NpmAvailable = false };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, runner.InstallCallCount);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_ForPathTraversalName()
    {
        var host = CreateHost();
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync("..\\escape", Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, runner.InstallCallCount);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_AndDoesNotPromote_OnIntegrityMismatch()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ResolvedIntegrityOverride = "sha512-different==" };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_WhenResolvedIntegrityIsUnknown()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ReturnNullIntegrity = true };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
    }

    [TestMethod]
    public async Task InstallAsync_Fails_OnManifestIdentityMismatch()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ManifestName = "@contoso/other" };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
    }

    [TestMethod]
    public async Task InstallAsync_Fails_OnManifestVersionMismatch()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ManifestVersion = "9.9.9" };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
    }

    [TestMethod]
    public async Task InstallAsync_RollsBack_WhenRegistrationTimesOut()
    {
        var host = CreateHost();
        host.RegistrationSucceeds = false;
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);

        // A promoted directory that never registered must be rolled back, not left behind.
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_Fails_AndCleansStaging_OnNpmFailure()
    {
        var host = CreateHost();
        var runner = new FakeRunner { NpmResult = NpmCommandResult.Fail("boom") };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_Cancelled_CleansStaging_AndLeavesNoTarget()
    {
        var host = CreateHost();
        var runner = new FakeRunner { ThrowCancelled = true };
        var installer = new NpmJsExtensionInstaller(host, runner);

        using var cts = new CancellationTokenSource();
        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, cts.Token);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(Directory.Exists(Path.Combine(host.ExtensionsRootPath, ExtensionName)));
        AssertStagingEmpty(host);
    }

    [TestMethod]
    public async Task InstallAsync_Blocks_WhenTargetAlreadyExists_AndPreservesIt()
    {
        var host = CreateHost();
        var target = Path.Combine(host.ExtensionsRootPath, ExtensionName);
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "existing.txt");
        File.WriteAllText(sentinel, "keep me");

        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, runner.InstallCallCount);

        // The pre-existing install must be untouched by a blocked reinstall.
        Assert.IsTrue(File.Exists(sentinel));
        Assert.AreEqual("keep me", File.ReadAllText(sentinel));
    }

    [TestMethod]
    public async Task InstallAsync_Blocks_WhenHostReportsInstalled()
    {
        var host = CreateHost();
        host.MarkInstalled(ExtensionName);
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, runner.InstallCallCount);
    }

    [TestMethod]
    public async Task InstallAsync_AwaitsDelayedRegistration_ThenSucceeds()
    {
        var host = CreateHost();
        host.RegistrationDelay = TimeSpan.FromMilliseconds(75);
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        Assert.IsTrue(host.IsExtensionInstalled(ExtensionName));
    }

    [TestMethod]
    public async Task UninstallAsync_StopsExtension_BeforeRemovingDirectory()
    {
        var order = new ConcurrentQueue<string>();
        var host = CreateHost();
        host.OrderLog = order;
        var target = Path.Combine(host.ExtensionsRootPath, ExtensionName);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "package.json"), "{}");

        var runner = new FakeRunner { OrderLog = order };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.UninstallAsync(ExtensionName, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        CollectionAssert.AreEqual(StopThenRemove, order.ToArray());
        Assert.IsFalse(Directory.Exists(target));
    }

    [TestMethod]
    public async Task UninstallAsync_Fails_WhenRemoveFails()
    {
        var host = CreateHost();
        var runner = new FakeRunner { RemoveSucceeds = false };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.UninstallAsync(ExtensionName, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual(1, host.StopCallCount);
    }

    [TestMethod]
    public async Task UninstallAsync_CancelDuringStop_ReturnsCanceled_AndDoesNotRemove()
    {
        var host = CreateHost();
        var target = Path.Combine(host.ExtensionsRootPath, ExtensionName);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "package.json"), "{}");

        using var stopStarted = new ManualResetEventSlim(false);
        host.StopHook = token =>
        {
            // Simulate the host blocking while it stops the provider, then observe the cancel that
            // arrives after the operation has already begun. This mirrors the real host threading the
            // uninstall token into its stop/delete steps.
            stopStarted.Set();
            Assert.IsTrue(token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)), "Cancellation was not observed during stop.");
            token.ThrowIfCancellationRequested();
        };

        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        using var cts = new CancellationTokenSource();
        var task = Task.Run(() => installer.UninstallAsync(ExtensionName, cts.Token));

        Assert.IsTrue(stopStarted.Wait(TimeSpan.FromSeconds(5)), "Uninstall did not reach the stop step.");
        cts.Cancel();

        var result = await task;

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, runner.RemoveCallCount, "Cancel during stop must not proceed to delete.");
        Assert.IsTrue(Directory.Exists(target), "The extension directory must remain when uninstall is canceled.");
    }

    [TestMethod]
    public async Task UninstallAsync_JunctionedRoot_IsRefused()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmdpal-installer-tests", Guid.NewGuid().ToString("N"));
        _tempRoots.Add(root);
        var realRoot = Path.Combine(root, "real");
        var junctionRoot = Path.Combine(root, "JSExtensions");
        Directory.CreateDirectory(realRoot);

        if (!TryCreateJunction(junctionRoot, realRoot))
        {
            Assert.Inconclusive("Could not create a junction on this machine.");
            return;
        }

        var host = new FakeHost(junctionRoot);
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.UninstallAsync(ExtensionName, CancellationToken.None);

        Assert.IsFalse(result.Succeeded, "A reparse-point extensions root must be refused.");
        Assert.AreEqual(0, host.StopCallCount);
        Assert.AreEqual(0, runner.RemoveCallCount);
    }

    [TestMethod]
    public async Task InstallAsync_JunctionedRoot_IsRefused()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmdpal-installer-tests", Guid.NewGuid().ToString("N"));
        _tempRoots.Add(root);
        var realRoot = Path.Combine(root, "real");
        var junctionRoot = Path.Combine(root, "JSExtensions");
        Directory.CreateDirectory(realRoot);

        if (!TryCreateJunction(junctionRoot, realRoot))
        {
            Assert.Inconclusive("Could not create a junction on this machine.");
            return;
        }

        var host = new FakeHost(junctionRoot);
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded, "A reparse-point extensions root must be refused.");
        Assert.AreEqual(0, runner.InstallCallCount, "npm must not run when the root is refused.");
    }

    [TestMethod]
    public async Task InstallAsync_CanceledDuringRegistration_RollsBack_StopThenRemove()
    {
        var order = new ConcurrentQueue<string>();
        var host = CreateHost();
        host.OrderLog = order;

        // Hold registration open long enough to cancel while the extension is already promoted.
        host.RegistrationDelay = TimeSpan.FromSeconds(30);
        using var registrationStarted = new ManualResetEventSlim(false);
        host.RegistrationStarted = registrationStarted;

        var runner = new FakeRunner { OrderLog = order };
        var installer = new NpmJsExtensionInstaller(host, runner);

        using var cts = new CancellationTokenSource();
        var task = Task.Run(() => installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, cts.Token));

        Assert.IsTrue(registrationStarted.Wait(TimeSpan.FromSeconds(5)), "Install did not reach provider registration.");
        cts.Cancel();

        var result = await task;

        Assert.IsFalse(result.Succeeded, "A canceled install must not report success.");

        // Rollback must stop the host before removing the promoted directory so nothing is left both
        // installed and running.
        var log = order.ToArray();
        Assert.IsTrue(log.Length >= 2, "Rollback did not run stop and remove.");
        Assert.AreEqual("stop", log[0]);
        Assert.AreEqual("remove", log[1]);
        Assert.AreEqual(1, host.StopCallCount);

        var target = Path.Combine(host.ExtensionsRootPath, ExtensionName);
        Assert.IsFalse(Directory.Exists(target), "The promoted directory must be removed on rollback.");
        Assert.IsFalse(host.IsExtensionInstalled(ExtensionName), "Nothing must remain installed after a canceled install.");
    }

    [TestMethod]
    public async Task InstallAsync_PreservesMixedScopedAndUnscopedDependencies()
    {
        // Evidence for r2-p5-07: phase-5 AssembleDiscoveryLayout moves whole top-level node_modules
        // entries, including @scope directories, so scoped, unscoped, and mixed dependencies all
        // survive promotion. The scoped-merge discard defect lives only in the phase-6
        // JsExtensionPackageLayout, which is absent from phase-5.
        var host = CreateHost();
        var runner = new FakeRunner
        {
            HoistedDependencies = ["left-pad", Path.Combine("@scope", "dep-a"), Path.Combine("@scope", "dep-b")],
        };
        var installer = new NpmJsExtensionInstaller(host, runner);

        var result = await installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);

        var nodeModules = Path.Combine(host.ExtensionsRootPath, ExtensionName, "node_modules");
        Assert.IsTrue(Directory.Exists(Path.Combine(nodeModules, "left-pad")), "Unscoped dependency was dropped.");
        Assert.IsTrue(Directory.Exists(Path.Combine(nodeModules, "@scope", "dep-a")), "Scoped dependency dep-a was dropped.");
        Assert.IsTrue(Directory.Exists(Path.Combine(nodeModules, "@scope", "dep-b")), "Scoped dependency dep-b was dropped.");
    }

    [TestMethod]
    public void IsInstalled_DelegatesToHost()
    {
        var host = CreateHost();
        host.MarkInstalled(ExtensionName);
        var installer = new NpmJsExtensionInstaller(host, new FakeRunner());

        Assert.IsTrue(installer.IsInstalled(ExtensionName));
        Assert.IsFalse(installer.IsInstalled("not-installed"));
    }

    [TestMethod]
    public async Task ConcurrentInstall_SameName_SerializesAndBlocksTheSecond()
    {
        var host = CreateHost();
        var runner = new FakeRunner();
        var installer = new NpmJsExtensionInstaller(host, runner);

        var first = installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);
        var second = installer.InstallAsync(ExtensionName, Package, Version, ValidIntegrity, null, CancellationToken.None);

        var results = await Task.WhenAll(first, second);

        var succeeded = 0;
        var blocked = 0;
        foreach (var result in results)
        {
            if (result.Succeeded)
            {
                succeeded++;
            }
            else
            {
                blocked++;
            }
        }

        // Serialization plus the block-if-exists upgrade policy means exactly one install runs npm and
        // succeeds while the other is refused, and npm is invoked only once.
        Assert.AreEqual(1, succeeded);
        Assert.AreEqual(1, blocked);
        Assert.AreEqual(1, runner.InstallCallCount);
    }

    [TestMethod]
    public async Task ConcurrentInstall_DifferentNames_RunInParallel()
    {
        var host = CreateHost();
        using var bothEntered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim(false);

        var runner = new FakeRunner
        {
            InstallHook = (_, _, hookToken) =>
            {
                bothEntered.Signal();

                // Block until both installs have entered npm. If the installer serialized different
                // directories this would deadlock and the wait below would time out.
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5), hookToken), "Second install did not run in parallel.");
                return Task.CompletedTask;
            },
        };

        var installer = new NpmJsExtensionInstaller(host, runner);

        var first = Task.Run(() => installer.InstallAsync("ext-one", Package, Version, ValidIntegrity, null, CancellationToken.None));
        var second = Task.Run(() => installer.InstallAsync("ext-two", "left-pad", "1.3.0", ValidIntegrity, null, CancellationToken.None));

        var parallel = bothEntered.Wait(TimeSpan.FromSeconds(5));
        release.Set();

        Assert.IsTrue(parallel, "Two installs of different extensions did not enter npm concurrently.");

        var results = await Task.WhenAll(first, second);
        Assert.IsTrue(results[0].Succeeded, results[0].ErrorMessage);
        Assert.IsTrue(results[1].Succeeded, results[1].ErrorMessage);
    }

    private FakeHost CreateHost()
    {
        var root = Path.Combine(Path.GetTempPath(), "cmdpal-installer-tests", Guid.NewGuid().ToString("N"));
        var extensionsRoot = Path.Combine(root, "JSExtensions");
        Directory.CreateDirectory(extensionsRoot);
        _tempRoots.Add(root);
        return new FakeHost(extensionsRoot);
    }

    private static void AssertStagingEmpty(FakeHost host)
    {
        var stagingRoot = Path.GetFullPath(host.ExtensionsRootPath) + ".staging";
        if (Directory.Exists(stagingRoot))
        {
            var leftovers = Directory.GetFileSystemEntries(stagingRoot);
            Assert.AreEqual(0, leftovers.Length, $"Staging root '{stagingRoot}' still holds {leftovers.Length} entries.");
        }
    }

    private static void TryDeleteTree(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var staging = root + ".staging";
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
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

    private sealed class FakeHost : IJsExtensionHost
    {
        private readonly HashSet<string> _installed = new(StringComparer.OrdinalIgnoreCase);
        private readonly Lock _installedGate = new();

        public FakeHost(string extensionsRootPath)
        {
            ExtensionsRootPath = extensionsRootPath;
        }

        public string ExtensionsRootPath { get; }

        public bool RegistrationSucceeds { get; set; } = true;

        public TimeSpan RegistrationDelay { get; set; } = TimeSpan.Zero;

        public int StopCallCount { get; private set; }

        public ConcurrentQueue<string>? OrderLog { get; set; }

        public Action<CancellationToken>? StopHook { get; set; }

        public ManualResetEventSlim? RegistrationStarted { get; set; }

        public void MarkInstalled(string name)
        {
            lock (_installedGate)
            {
                _installed.Add(name);
            }
        }

        public void StopExtension(string extensionDirectory, CancellationToken cancellationToken = default)
        {
            OrderLog?.Enqueue("stop");
            StopCallCount++;
            StopHook?.Invoke(cancellationToken);
        }

        public bool IsExtensionDiscoverable(string extensionDirectory) =>
            File.Exists(Path.Combine(extensionDirectory, "package.json"));

        public bool IsExtensionInstalled(string extensionName)
        {
            lock (_installedGate)
            {
                return _installed.Contains(extensionName);
            }
        }

        public async Task<bool> RefreshAndAwaitProviderAsync(string extensionDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            RegistrationStarted?.Set();

            if (RegistrationDelay > TimeSpan.Zero)
            {
                await Task.Delay(RegistrationDelay, cancellationToken).ConfigureAwait(false);
            }

            if (!RegistrationSucceeds)
            {
                return false;
            }

            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(extensionDirectory));
            lock (_installedGate)
            {
                _installed.Add(name);
            }

            return true;
        }
    }

    private sealed class FakeRunner : INpmCommandRunner
    {
        public bool NpmAvailable { get; set; } = true;

        public bool RemoveSucceeds { get; set; } = true;

        public bool CreatePackage { get; set; } = true;

        public bool ThrowCancelled { get; set; }

        public bool ReturnNullIntegrity { get; set; }

        public string? ResolvedIntegrityOverride { get; set; }

        public string? ManifestName { get; set; }

        public string? ManifestVersion { get; set; }

        public string[] HoistedDependencies { get; set; } = [];

        public NpmCommandResult? NpmResult { get; set; }

        public Func<string, NpmArtifact, CancellationToken, Task>? InstallHook { get; set; }

        public ConcurrentQueue<string>? OrderLog { get; set; }

        public int InstallCallCount { get; private set; }

        public int RemoveCallCount { get; private set; }

        public List<string> StagingDirectories { get; } = new();

        public bool IsNpmAvailable() => NpmAvailable;

        public async Task<NpmCommandResult> InstallAsync(string stagingDirectory, NpmArtifact artifact, CancellationToken cancellationToken)
        {
            InstallCallCount++;
            lock (StagingDirectories)
            {
                StagingDirectories.Add(stagingDirectory);
            }

            if (InstallHook is not null)
            {
                await InstallHook(stagingDirectory, artifact, cancellationToken).ConfigureAwait(false);
            }

            if (ThrowCancelled)
            {
                throw new OperationCanceledException();
            }

            if (NpmResult is { } forced)
            {
                return forced;
            }

            if (CreatePackage)
            {
                MaterializePackage(stagingDirectory, artifact);
            }

            if (ReturnNullIntegrity)
            {
                return NpmCommandResult.Ok(null);
            }

            return NpmCommandResult.Ok(ResolvedIntegrityOverride ?? artifact.Integrity);
        }

        public bool RemoveDirectory(string targetDirectory, CancellationToken cancellationToken = default)
        {
            OrderLog?.Enqueue("remove");
            RemoveCallCount++;

            cancellationToken.ThrowIfCancellationRequested();

            if (!RemoveSucceeds)
            {
                return false;
            }

            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            return true;
        }

        private void MaterializePackage(string stagingDirectory, NpmArtifact artifact)
        {
            var relative = artifact.Package.Replace('/', Path.DirectorySeparatorChar);
            var packageDirectory = Path.Combine(stagingDirectory, "node_modules", relative);
            Directory.CreateDirectory(packageDirectory);

            File.WriteAllText(Path.Combine(packageDirectory, "index.js"), "module.exports = {};");

            var name = ManifestName ?? artifact.Package;
            var version = ManifestVersion ?? artifact.Version;
            var manifest = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"name\":\"{0}\",\"version\":\"{1}\",\"main\":\"index.js\",\"cmdpal\":{{}}}}",
                name,
                version);
            File.WriteAllText(Path.Combine(packageDirectory, "package.json"), manifest);

            foreach (var dependency in HoistedDependencies)
            {
                var dependencyDirectory = Path.Combine(stagingDirectory, "node_modules", dependency);
                Directory.CreateDirectory(dependencyDirectory);
                File.WriteAllText(Path.Combine(dependencyDirectory, "index.js"), "module.exports = {};");
            }
        }
    }
}
