// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class RecoveryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MwbRecovery")]
    [TestCategory("NestedSandboxDebugPilot")]
    public void ControllerExitBeforePairingRestoresOriginalSettings()
    {
        using var fixture = new EndpointRecoveryFixture(TestContext, clipboardLost: false);
        fixture.StartBeforePairing();
        fixture.ExpireOwnerLease();
        fixture.AssertRecovery();
    }

    [TestMethod]
    [TestCategory("MwbRecovery")]
    [TestCategory("NestedSandboxDebugPilot")]
    public void KilledClipboardOwnerRequiresBaselineReset()
    {
        using var fixture = new EndpointRecoveryFixture(TestContext, clipboardLost: true);
        fixture.StartBeforePairing();
        fixture.KillClipboardOwner();
        fixture.AssertRecovery();
    }

    [TestMethod]
    [TestCategory("MwbRecovery")]
    [TestCategory("NestedSandboxDebugPilot")]
    public void AbortedSandboxStartupRefusesUnownedInstance()
    {
        WinAppSandboxPrerequisiteReport.CapturePersistent(
            TestContext.TestRunDirectory, TestContext.AddResultFile, ExerciseSandboxOwnership);
    }

    [TestMethod]
    [TestCategory("MwbRecovery")]
    [TestCategory("NestedSandboxDebugPilot")]
    public void StaleRunDirectoryRefusalPreservesRecoveryJournals()
    {
        var root = Path.Combine(RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory), "mwb-stale-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var snapshots = new Dictionary<string, byte[]>
        {
            ["cleanup-journal.json"] = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"RunId\":\"prior-owner\",\"Status\":\"RecoveryRequired\",\"TestProcess\":{\"Id\":123,\"StartTimeUtc\":\"2026-10-01T00:00:00Z\"}}\r\n")).ToArray(),
            ["phases.json"] = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{\"RunId\":\"prior-owner\",\"Phases\":[{\"Name\":\"Prior failure\",\"Status\":\"FAIL\"}]}\r\n")).ToArray(),
            ["prior-evidence.json"] = Encoding.UTF8.GetBytes("{\"RunId\":\"prior-owner\",\"MustRemainPrivate\":true}"),
        };
        foreach (var (name, bytes) in snapshots)
        {
            File.WriteAllBytes(Path.Combine(root, name), bytes);
            File.WriteAllText(Path.Combine(root, name + ".ready"), "prior publication");
        }

        var filesBefore = Directory.GetFiles(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        var previousRoot = Environment.GetEnvironmentVariable("POWERTOYS_MWB_RUN_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("POWERTOYS_MWB_RUN_ROOT", root);
            WinAppSandboxPrerequisiteReport.CapturePersistent(TestContext.TestRunDirectory, TestContext.AddResultFile, report =>
            {
                using var fixture = new TwoEndpointFixture(TestContext, report);
                var refusal = Assert.ThrowsExactly<AssertFailedException>(fixture.Run);
                StringAssert.Contains(refusal.Message, "Run directory must be new; preserve old recovery journals.");
                Assert.AreEqual(0, fixture.Cleanup().Count, "Refusal cleanup must not act on the stale directory.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POWERTOYS_MWB_RUN_ROOT", previousRoot);
        }

        CollectionAssert.AreEqual(filesBefore, Directory.GetFiles(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        foreach (var (name, bytes) in snapshots)
        {
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(root, name)), "Refusal/finally/cleanup modified a prior-owner file: " + name);
            Assert.AreEqual("prior publication", File.ReadAllText(Path.Combine(root, name + ".ready")));
        }

        var evidence = Path.Combine(RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory), "mwb-stale-refusal-" + Guid.NewGuid().ToString("N") + ".json");
        RunFiles.Write(evidence, new
        {
            Scenario = nameof(StaleRunDirectoryRefusalPreservesRecoveryJournals),
            FailureVerdict = "ExpectedFailure",
            ExpectedFailure = "Nonempty stale run directory refused",
            RecoveryVerdict = "UnownedRecoveryStatePreserved",
            PhaseFinallyAndCleanupExercised = true,
            StaleFilesUnchanged = true,
            NewFilesInStaleRoot = 0,
            Files = snapshots.Select(item => new { Name = item.Key, Sha256 = Convert.ToHexString(SHA256.HashData(item.Value)) }).ToArray(),
        });
        TestContext.AddResultFile(evidence);
    }

    private void ExerciseSandboxOwnership(WinAppSandboxPrerequisiteReport prerequisites)
    {
        var (_, provision, _, runId) = EndpointRecoveryFixture.ReadProvisioning();
        prerequisites.SetRunId(runId);
        var backend = SandboxBackendSelection.Resolve(provision["SandboxBackend"]?.GetValue<string>(), Environment.OSVersion.Version.Build);
        var watch = Stopwatch.StartNew();
        var root = Path.Combine(
            RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory),
            "mwb-recovery-sandbox-" + Guid.NewGuid().ToString("N"));
        var control = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToysUiTestControl",
            runId,
            "sandbox-recovery-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(AppContext.BaseDirectory, "Payload");
        var archive = provision["GuestArchivePath"]!.GetValue<string>();
        var tools = Path.GetDirectoryName(Environment.GetEnvironmentVariable("WINAPP_CLI_PATH"))!;
        var originalSettings = EndpointRecoveryFixture.SettingsFingerprint();
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(control);
        using var recordings = new TestRecordings(TestContext, Path.Combine(root, "recordings"));
        ISandboxSession? owned = null;
        ISandboxSession? rejected = null;
        var expected = new RecoveryBoundaryException();
        var refusingId = Guid.NewGuid().ToString();
        var refusalObserved = false;
        var ownedIdentityPreserved = false;
        var recovered = false;
        var deadline = DateTime.UtcNow.AddMinutes(8);
        var archiveHash = provision["GuestArchiveSha256"]!.GetValue<string>();
        var guest = Channel(runId, control, Path.Combine(root, "guest"), deadline, archiveHash);
        var refusingControl = Path.Combine(control, "unowned-attempt");
        var refusingRoot = Path.Combine(root, "unowned-attempt");
        var refusingGuest = Channel(refusingId, refusingControl, Path.Combine(refusingRoot, "guest"), deadline, archiveHash);
        void Save() => RunFiles.Write(Path.Combine(root, "owned-sandbox.json"), new
        {
            RunId = runId, Backend = backend, Phase = "Created before guest acknowledgement",
            Processes = owned?.Processes, ModernSandbox = owned?.RecoveryState, ViewerHwnd = owned?.ViewerHwnd,
            GuestAcknowledged = owned?.GuestAcknowledged,
        });
        ISandboxSession Session(string id, string privateRoot, string results, Action? created) => backend == "Legacy"
            ? new LegacySandbox(Save, recordings.CaptureSandboxWindow, created)
            : new WinAppSandbox(id, privateRoot, results, provision["SandboxWinAppPath"]!.GetValue<string>(), Save, TestContext, prerequisites, created);
        void AbortCreatedInstance()
        {
            Assert.IsFalse(owned!.GuestAcknowledged);
            ProcessIdentity[] identity = [];
            if (owned is LegacySandbox legacy)
            {
                RunFiles.Wait(
                    () => legacy.ViewerHwnd != 0,
                    TimeSpan.FromSeconds(90),
                    "The owned legacy startup did not create its viewer.",
                    legacy.Discover);
                identity = legacy.Processes.ToArray();
                Assert.IsTrue(identity.Any(record => record.IsCurrent()));
            }
            else
            {
                CollectionAssert.AreEqual(new[] { Guid.Parse(runId) }, ((WinAppSandbox)owned).RecoveryInventory());
                Assert.IsTrue(owned.RecoveryState!["CreationConfirmed"]!.GetValue<bool>());
            }

            rejected = Session(refusingId, refusingControl, refusingRoot, created: null);
            if (backend == "Legacy")
            {
                var refusal = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    rejected.Start(Path.Combine(refusingRoot, "run.wsb"), archive, payload, tools, refusingGuest));
                StringAssert.Contains(refusal.Message, "pre-existing Windows Sandbox");
                Assert.AreEqual(0, rejected.Processes.Count);
                ownedIdentityPreserved = identity.All(record => record.IsCurrent());
            }
            else
            {
                var refusal = Assert.ThrowsExactly<AggregateException>(() =>
                    rejected.Start(Path.Combine(refusingRoot, "run.wsb"), archive, payload, tools, refusingGuest));
                var errors = refusal.Flatten().InnerExceptions;
                Assert.AreEqual(1, errors.Count, "Only the expected unowned-instance refusal may be accepted.");
                Assert.IsInstanceOfType<WinAppSandboxException>(errors[0]);
                Assert.AreEqual("preexisting_sandbox", ((WinAppSandboxException)errors[0]).Code);
                Assert.IsFalse(rejected.RecoveryState!["CreationAttempted"]!.GetValue<bool>());
                ownedIdentityPreserved = ((WinAppSandbox)owned).RecoveryInventory().SequenceEqual([Guid.Parse(runId)]);
            }

            Assert.IsTrue(ownedIdentityPreserved, "Refusal must not close, adopt, or replace the pre-existing instance.");
            refusalObserved = true;
            RunFiles.Write(Path.Combine(root, "unowned-refusal.json"), new
            {
                RunId = runId, RefusingRunId = refusingId, Backend = backend, ExpectedFailure = "preexisting_sandbox",
                OwnedIdentityPreserved = true, RefusedBeforeCreation = true, UnownedInstanceStopped = false,
                PreexistingProcesses = identity, PreexistingInstanceId = backend == "WinApp" ? runId : null,
            });
            throw expected;
        }

        try
        {
            recordings.StartDesktop();
            owned = Session(runId, control, root, AbortCreatedInstance);
            var actual = Assert.ThrowsExactly<RecoveryBoundaryException>(() =>
                owned.Start(Path.Combine(root, "run.wsb"), archive, payload, tools, guest));
            Assert.AreSame(expected, actual, "An unrelated startup failure must not satisfy fault injection.");
            Assert.IsTrue(refusalObserved && ownedIdentityPreserved);
            Assert.IsFalse(owned.GuestAcknowledged, "This is an aborted startup, not another full smoke.");
            recordings.StopSandbox();
            owned.Stop();
            rejected!.Stop();
            if (owned is WinAppSandbox modern)
            {
                Assert.AreEqual(0, modern.RecoveryInventory().Length);
                Assert.IsTrue(modern.RecoveryState["Stopped"]!.GetValue<bool>());
            }
            else
            {
                Assert.IsTrue(owned.Processes.All(record => !record.IsCurrent()));
                LegacySandbox.AssertNoneRunning();
            }

            CollectionAssert.AreEquivalent(originalSettings.ToArray(), EndpointRecoveryFixture.SettingsFingerprint().ToArray());
            recovered = true;
            RunFiles.Write(Path.Combine(root, "recovery-scenario.json"), new
            {
                FormatVersion = 1, RunId = runId, Scenario = TestContext.TestName, Backend = backend,
                FailureVerdict = "ExpectedFailure", Fault = "Abort immediately after owned creation and unowned-instance refusal",
                RecoveryVerdict = "Recovered", RefusalObserved = true, OwnedIdentityPreserved = true,
                OwnedResourcesStopped = true, OriginalHostSettingsUnchanged = true,
                GuestAcknowledged = false, RequiresBaselineReset = false, ElapsedSeconds = watch.Elapsed.TotalSeconds,
                CaseHardDeadlineSeconds = 480,
            });
        }
        finally
        {
            try
            {
                owned?.Stop();
                rejected?.Stop();
                if (recovered)
                {
                    _ = WinAppSandboxPayload.PlainFiles(control).ToArray();
                    RunFiles.RemoveOwnedDirectory(control, TimeSpan.FromSeconds(45));
                    RunFiles.Write(Path.Combine(root, "test-isolation.json"), new
                    {
                        RunId = runId, OwnedControlRemoved = !Directory.Exists(control), OwnedResourcesAbsent = true,
                    });
                }
            }
            finally
            {
                recordings.Complete();
                foreach (var directory in new[] { root, refusingRoot })
                {
                    foreach (var file in RunFiles.EvidenceFiles(directory))
                    {
                        TestContext.AddResultFile(file);
                    }
                }
            }
        }
    }

    private static EndpointChannel Channel(string id, string control, string output, DateTime deadline, string archiveHash)
    {
        var channel = new EndpointChannel(id, Path.Combine(control, "guest-input"), output);
        RunFiles.Write(Path.Combine(channel.InputRoot, "bootstrap.json"), new
        {
            RunId = id, Role = "Guest", HardDeadlineUtc = deadline, GuestArchiveSha256 = archiveHash, Manifest = Array.Empty<object>(),
        });
        channel.BeginBootstrap(modern: true);
        channel.WriteLease();
        return channel;
    }

    private sealed class RecoveryBoundaryException : Exception
    {
    }
}
