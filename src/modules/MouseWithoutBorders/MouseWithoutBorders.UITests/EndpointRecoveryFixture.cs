// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class EndpointRecoveryFixture : IDisposable
{
    private readonly TestContext context;
    private readonly bool clipboardLost;
    private readonly string payloadRoot = Path.Combine(AppContext.BaseDirectory, "Payload");
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private readonly List<SettingsSnapshot> settings;
    private readonly EndpointChannel host;
    private readonly TestRecordings recordings;
    private readonly Process controller;
    private readonly string controlRoot;
    private readonly string markerPath;
    private readonly JsonObject provision;
    private ReceiverController? clipboardGuard;
    private ProcessIdentity? worker;
    private JsonObject? recovery;
    private string? publishedDigest;
    private bool disposed;

    internal EndpointRecoveryFixture(TestContext context, bool clipboardLost)
    {
        this.context = context;
        this.clipboardLost = clipboardLost;
        (markerPath, provision, ProductRoot, RunId) = ReadProvisioning();
        RunRoot = Path.Combine(
            RunFiles.PersistentResultsRoot(context.TestRunDirectory),
            "mwb-recovery-" + Guid.NewGuid().ToString("N"));
        controlRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToysUiTestControl",
            RunId,
            "recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RunRoot);
        Directory.CreateDirectory(controlRoot);
        settings = SettingsPaths().Select(path => new SettingsSnapshot(path, File.Exists(path), File.Exists(path) ? Hash(path) : null)).ToList();
        Assert.IsFalse(Directory.Exists(BackupRoot), "A previous run's private settings recovery must be resolved first.");
        host = new EndpointChannel(RunId, Path.Combine(controlRoot, "host-input"), Path.Combine(RunRoot, "host"));
        var deadline = DateTime.UtcNow.AddMinutes(8);
        RunFiles.Write(Path.Combine(host.InputRoot, "bootstrap.json"), new
        {
            RunId, Role = "Host", HardDeadlineUtc = deadline,
            Manifest = new[] { new { Path = "PowerToys.MouseWithoutBorders.dll", Sha256 = Hash(Path.Combine(ProductRoot, "PowerToys.MouseWithoutBorders.dll")) } },
        });
        host.BeginBootstrap();
        host.WriteLease();
        var configurationPath = Path.Combine(controlRoot, "controller.json");
        RunFiles.Write(configurationPath, new
        {
            RunId, RunRoot, ControlRoot = controlRoot, ProductRoot, ProvisioningMarker = markerPath,
            InputRoot = host.InputRoot, HardDeadlineUtc = deadline,
            WinApp = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH"),
            Parent = ProcessIdentity.Capture(Environment.ProcessId),
        });
        recordings = new TestRecordings(context, Path.Combine(RunRoot, "recordings"));
        controller = StartScript("RecoveryController.ps1", "-ConfigurationPath", configurationPath);
    }

    internal string RunId { get; }

    internal string RunRoot { get; }

    internal string ProductRoot { get; }

    private string BackupRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToysUiTestRecovery",
        RunId);

    internal static (string Marker, JsonObject Provision, string Product, string RunId) ReadProvisioning()
    {
        var desktop = NativeSupport.Desktop();
        Assert.IsTrue(desktop.Ready && !desktop.Elevated, "Recovery tests require the original unlocked standard-user desktop.");
        var marker = Environment.GetEnvironmentVariable("POWERTOYS_MWB_PROVISIONING")
            ?? @"C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json";
        var provision = RunFiles.Read(marker);
        using var identity = WindowsIdentity.GetCurrent();
        Assert.AreEqual(identity.User!.Value, provision["TestUserSid"]?.GetValue<string>());
        Assert.IsTrue(provision["Status"]?.GetValue<string>() is "Ready" or "WaitingForSandbox");
        var product = Path.GetFullPath(Environment.GetEnvironmentVariable("POWERTOYS_INSTALL_DIR")
            ?? throw new InvalidOperationException("Missing protected Debug product staging.")).TrimEnd('\\');
        Assert.IsTrue(string.Equals(product, provision[nameof(ProductRoot)]!.GetValue<string>().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        foreach (var path in new[] { "PowerToys.MouseWithoutBorders.dll", @"WinUI3Apps\PowerToys.Settings.dll" })
        {
            TwoEndpointFixture.AssertDebugAssembly(Path.Combine(product, path));
        }

        foreach (var (field, file) in new[] { ("ExecutableSha256", "PowerToys.MouseWithoutBorders.exe"), ("LibrarySha256", "PowerToys.MouseWithoutBorders.dll") })
        {
            if (provision[field] is not null)
            {
                Assert.IsTrue(string.Equals(Hash(Path.Combine(product, file)), provision[field]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
            }
        }

        if (provision["SandboxBackend"]?.GetValue<string>() == "WinApp")
        {
            var sandboxCliHash = Hash(provision["SandboxWinAppPath"]!.GetValue<string>());
            Assert.IsTrue(string.Equals(sandboxCliHash, provision["SandboxWinAppSha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
        }

        AssertNoProductProcesses();
        return (marker, provision, product, Guid.Parse(provision[nameof(RunId)]!.GetValue<string>()).ToString());
    }

    internal void StartBeforePairing()
    {
        EnsureProtectedNetwork();
        RunFiles.Wait(
            () => File.Exists(Path.Combine(RunRoot, "controller-ready.json.ready")),
            Budget(30),
            "The owned recovery controller did not initialize.",
            CheckController);
        var journal = RunFiles.Read(Path.Combine(RunRoot, "cleanup-journal.json"));
        var owner = ReadIdentity(journal["TestProcess"]!);
        Assert.AreEqual(controller.Id, owner.Id);
        Assert.AreEqual(controller.StartTime.ToUniversalTime(), owner.StartTimeUtc);
        Assert.AreEqual(Environment.ProcessId, owner.ParentId);
        worker = ReadIdentity(journal["HostWorker"]!);
        Assert.AreEqual(owner.Id, worker.ParentId);
        Assert.AreEqual(owner.SessionId, worker.SessionId);
        var publisherPath = Path.Combine(RunRoot, "Host-lease-publisher.json");
        RunFiles.Wait(
            () => File.Exists(publisherPath + ".ready") && RunFiles.Read(publisherPath)["Stage"]?.GetValue<string>() == "Published",
            Budget(30),
            "The owned lease publisher did not publish a live generation.",
            CheckController);
        if (clipboardLost)
        {
            // This independent, in-memory guard is test isolation, not a recovery fallback.
            // Recover-Host must still report that the killed worker lost its own snapshot.
            clipboardGuard = new ReceiverController("Host", RunId);
            clipboardGuard.PublishClipboard();
        }

        var ready = host.WaitForReady(CheckController);
        Assert.AreEqual(worker.Id, ready["Worker"]!["Id"]!.GetValue<int>());
        var started = host.Request(
            "Start",
            new JsonObject
            {
                ["PeerName"] = "MWB-RECOVERY-ABSENT", ["PeerAddress"] = "192.0.2.1",
                ["ExpectedLocalAddress"] = IPAddress.Loopback.ToString(),
                ["GlobalSettings"] = TwoEndpointFixture.CreateGlobalSettings(payloadRoot),
            },
            timeoutSeconds: (int)Budget(360).TotalSeconds);
        var endpoint = RunFiles.Read(Path.Combine(host.OutputRoot, "endpoint-journal.json"));
        Assert.AreEqual(3, endpoint["Settings"]!.AsArray().Count);
        Assert.IsFalse(endpoint["SettingsRestored"]!.GetValue<bool>());
        Assert.IsTrue(settings.Any(snapshot => !snapshot.Matches()), "The real startup must alter the scoped settings before recovery.");
        var owned = endpoint["Processes"]!.AsArray().Select(node => ReadIdentity(node!)).ToArray();
        Assert.IsTrue(
            owned.Any(record => record.Id == started["MwbProcessId"]!.GetValue<int>() && record.IsCurrent()),
            "The real MWB process must still match its full recorded birth identity before injection.");
        Assert.IsTrue(
            owned.Any(record => record.Id == started["SettingsProcessId"]!.GetValue<int>() && record.IsCurrent()),
            "The real Settings process must still match its full recorded birth identity before injection.");
        recordings.StartDesktop();
        RunFiles.Write(Path.Combine(RunRoot, "fault-boundary.json"), new
        {
            RunId, Boundary = "Real host startup complete, before pairing", Owner = owner, Worker = worker,
            SettingsChanged = true, Peers = owned, PairingAttempted = false,
        });
    }

    internal void ExpireOwnerLease()
    {
        StopController();
        var publisherPath = Path.Combine(RunRoot, "Host-lease-publisher.json");
        RunFiles.Wait(
            () => RunFiles.Read(publisherPath)["Stage"]?.GetValue<string>() == "ParentExited",
            Budget(15),
            "The lease publisher did not observe its retained owner exit.");
        var lastPublished = RunFiles.Read(publisherPath)["LastPublishedUtc"]!.GetValue<DateTime>().ToUniversalTime();
        RunFiles.Wait(() => !worker!.IsCurrent(), Budget(95), "The dead lease did not stop its owned endpoint.");
        var failurePath = Path.Combine(host.OutputRoot, "failed.json");
        var failure = RunFiles.Read(failurePath);
        Assert.AreEqual(RunId, failure[nameof(RunId)]!.GetValue<string>());
        StringAssert.StartsWith(failure["Error"]!.GetValue<string>(), "Host lease expired;");
        Assert.IsTrue(
            (File.GetLastWriteTimeUtc(failurePath) - lastPublished).TotalSeconds >= 45,
            "The recovery test must exercise the real 45-second watchdog, not back-date a lease.");
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => host.Request("Observe", timeoutSeconds: 5));
        StringAssert.Contains(error.Message, "Host lease expired;");
        var exited = RunFiles.Read(Path.Combine(host.OutputRoot, "worker-exited.json"));
        Assert.AreEqual(RunId, exited[nameof(RunId)]!.GetValue<string>());
        Assert.IsFalse(exited["Cleaned"]!.GetValue<bool>(), "An aborted controller must not masquerade as normal Finish cleanup.");
    }

    internal void KillClipboardOwner()
    {
        var published = host.Request("PublishClipboard");
        var digest = published["Digest"]!.GetValue<string>();
        publishedDigest = digest;
        Assert.AreEqual(64, digest.Length);
        Assert.AreEqual(digest, clipboardGuard!.ClipboardDigest());
        var endpoint = RunFiles.Read(Path.Combine(host.OutputRoot, "endpoint-journal.json"));
        Assert.IsTrue(endpoint["ClipboardMayHaveChanged"]!.GetValue<bool>());
        Assert.IsFalse(endpoint["ClipboardRestored"]!.GetValue<bool>());
        worker!.Stop();
        Assert.IsFalse(worker.IsCurrent());
        StopController();
        Assert.IsFalse(
            File.Exists(Path.Combine(host.OutputRoot, "worker-exited.json.ready")),
            "A forcibly terminated owner must not manufacture a successful worker-finally journal.");
        RunFiles.Write(Path.Combine(RunRoot, "clipboard-owner-lost.json"), new
        {
            RunId, Worker = worker, WorkerExited = true, OriginalSnapshotPersisted = false,
            ClipboardMayHaveChanged = true, PublishedDigest = digest,
        });
    }

    internal void AssertRecovery()
    {
        recovery = Recover();
        RecoveryOutcome.Require(recovery, RunId, clipboardLost);
        if (clipboardLost)
        {
            Assert.AreEqual(publishedDigest, clipboardGuard!.ClipboardDigest(), "Recovery must not guess or substitute the lost clipboard.");
        }

        AssertRestoredSettings();
        AssertOwnedProcessesStopped();
        AssertNoProductProcesses();
        RunFiles.Write(Path.Combine(RunRoot, "recovery-scenario.json"), new
        {
            FormatVersion = 1, RunId, Scenario = context.TestName,
            FailureVerdict = "ExpectedFailure",
            Fault = clipboardLost ? "Killed original clipboard owner and controller" : "Exited controller; real 45-second lease expired",
            RecoveryVerdict = clipboardLost ? "RequiresBaselineReset" : "Recovered",
            RecoveryScriptStatus = recovery["Status"]!.GetValue<string>(),
            SettingsRestored = true, OwnedResourcesStopped = true, NormalFinishAcknowledged = false,
            RequiresBaselineReset = clipboardLost, ElapsedSeconds = watch.Elapsed.TotalSeconds,
            CaseHardDeadlineSeconds = 480, RecoveryDeadlineSeconds = 180, IndependentClipboardGuard = clipboardLost,
        });
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            StopController();
            recovery ??= Recover();
            AssertRestoredSettings();
            AssertOwnedProcessesStopped();
            AssertNoProductProcesses();
            clipboardGuard?.RestoreClipboard();
            if (Directory.Exists(BackupRoot))
            {
                var endpoint = RunFiles.Read(Path.Combine(host.OutputRoot, "endpoint-journal.json"));
                var expected = endpoint["Settings"]!.AsArray().Where(node => node!["Existed"]!.GetValue<bool>())
                    .Select(node => Path.GetFullPath(node!["Backup"]!.GetValue<string>())).ToArray();
                CollectionAssert.AreEquivalent(expected, WinAppSandboxPayload.PlainFiles(BackupRoot).ToArray());
                RunFiles.RemoveOwnedDirectory(BackupRoot, TimeSpan.FromSeconds(45));
            }

            _ = WinAppSandboxPayload.PlainFiles(controlRoot).ToArray();
            RunFiles.RemoveOwnedDirectory(controlRoot, TimeSpan.FromSeconds(45));
            RunFiles.Write(Path.Combine(RunRoot, "test-isolation.json"), new
            {
                RunId, SettingsStillOriginal = true, OwnedProcessesAbsent = true,
                PrivateBackupsRemoved = !Directory.Exists(BackupRoot), OwnedControlRemoved = !Directory.Exists(controlRoot),
                IndependentClipboardGuardRestored = clipboardLost,
                RecoveryRequiresBaselineReset = recovery["RequiresBaselineReset"]!.GetValue<bool>(),
            });
        }
        finally
        {
            recordings.Complete();
            recordings.Dispose();
            clipboardGuard?.Dispose();
            controller.Dispose();
            foreach (var directory in new[] { RunRoot, host.OutputRoot })
            {
                foreach (var file in RunFiles.EvidenceFiles(directory))
                {
                    context.AddResultFile(file);
                }
            }
        }
    }

    private JsonObject Recover()
    {
        using var process = StartScript(
            "Recover-Host.ps1",
            "-JournalPath",
            Path.Combine(RunRoot, "cleanup-journal.json"),
            "-ProvisioningMarker",
            markerPath);
        if (!process.WaitForExit(195_000))
        {
            process.Kill();
            Assert.IsTrue(process.WaitForExit(5000));
            Assert.Fail("The original-user recovery script exceeded its three-minute deadline.");
        }

        var result = RunFiles.Read(Path.Combine(RunRoot, "recovery-result.json"));
        Assert.AreEqual(result["Status"]?.GetValue<string>() == "Recovered" ? 0 : 1, process.ExitCode);
        return result;
    }

    private Process StartScript(string name, params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = RunRoot,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-STA", "-File", Path.Combine(payloadRoot, name) }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("Owned recovery script did not start.");
    }

    private void StopController()
    {
        if (!controller.HasExited)
        {
            controller.Kill();
            Assert.IsTrue(controller.WaitForExit(10_000), "The retained recovery controller handle did not exit.");
        }
    }

    private void CheckController()
    {
        Assert.IsFalse(controller.HasExited, "The recovery controller exited before fault injection.");
        var publisherPath = Path.Combine(RunRoot, "Host-lease-publisher.json");
        if (File.Exists(publisherPath + ".ready"))
        {
            var publisher = RunFiles.Read(publisherPath);
            Assert.AreEqual(RunId, publisher[nameof(RunId)]!.GetValue<string>());
            if (publisher["Stage"]?.GetValue<string>() == "Failed")
            {
                throw new InvalidOperationException("The lease publisher failed before fault injection: " + publisher["Error"]);
            }
        }
    }

    private void EnsureProtectedNetwork()
    {
        var current = RunFiles.Read(markerPath);
        Assert.AreEqual(RunId, current[nameof(RunId)]!.GetValue<string>());
        if (current["Status"]?.GetValue<string>() == "Ready")
        {
            return;
        }

        Assert.AreEqual("WaitingForSandbox", current["Status"]?.GetValue<string>());
        var networkControl = Path.Combine(controlRoot, "network-bootstrap");
        var networkOutput = Path.Combine(RunRoot, "network-bootstrap");
        Directory.CreateDirectory(networkControl);
        var guest = new EndpointChannel(RunId, Path.Combine(networkControl, "input"), networkOutput);
        RunFiles.Write(Path.Combine(guest.InputRoot, "bootstrap.json"), new
        {
            RunId, Role = "Guest", HardDeadlineUtc = DateTime.UtcNow + Budget(180),
            GuestArchiveSha256 = provision["GuestArchiveSha256"]!.GetValue<string>(), Manifest = Array.Empty<object>(),
        });
        guest.BeginBootstrap(modern: true);
        guest.WriteLease();
        ISandboxSession? network = null;
        var expected = new NetworkReadyBoundaryException();
        void Save() => RunFiles.Write(Path.Combine(RunRoot, "network-prerequisite.json"), new
        {
            RunId, Phase = "Protected Default Switch and exact firewall readiness",
            Processes = network?.Processes, ModernSandbox = network?.RecoveryState,
            ViewerHwnd = network?.ViewerHwnd, ProductStarted = false,
        });
        void Ready()
        {
            RunFiles.Wait(
                () =>
                {
                    var marker = RunFiles.Read(markerPath);
                    Assert.AreEqual(RunId, marker[nameof(RunId)]?.GetValue<string>());
                    Assert.AreEqual(provision["RuleName"]?.GetValue<string>(), marker["RuleName"]?.GetValue<string>());
                    Assert.IsTrue(marker["Status"]?.GetValue<string>() is "Ready" or "WaitingForSandbox");
                    return marker["Status"]?.GetValue<string>() == "Ready";
                },
                Budget(180),
                "Protected network setup did not become Ready before starting a real recovery peer.",
                () => (network as LegacySandbox)?.Discover());
            throw expected;
        }

        try
        {
            WinAppSandboxPrerequisiteReport.CapturePersistent(context.TestRunDirectory, context.AddResultFile, report =>
            {
                report.SetRunId(RunId);
                network = provision["SandboxBackend"]?.GetValue<string>() == "Legacy"
                    ? new LegacySandbox(Save, _ => { }, Ready)
                    : new WinAppSandbox(RunId, networkControl, networkOutput, provision["SandboxWinAppPath"]!.GetValue<string>(), Save, context, report, Ready);
                var actual = Assert.ThrowsExactly<NetworkReadyBoundaryException>(() => network.Start(
                    Path.Combine(networkOutput, "run.wsb"),
                    provision["GuestArchivePath"]!.GetValue<string>(),
                    payloadRoot,
                    Path.GetDirectoryName(Environment.GetEnvironmentVariable("WINAPP_CLI_PATH"))!,
                    guest));
                Assert.AreSame(expected, actual);
            });
        }
        finally
        {
            network?.Stop();
            RunFiles.RemoveOwnedDirectory(networkControl, TimeSpan.FromSeconds(45));
        }

        Assert.AreEqual("Ready", RunFiles.Read(markerPath)["Status"]?.GetValue<string>());
    }

    private TimeSpan Budget(int seconds)
    {
        var remaining = TimeSpan.FromMinutes(8) - watch.Elapsed;
        if (remaining < TimeSpan.FromSeconds(1))
        {
            throw new TimeoutException("The recovery scenario exceeded its eight-minute hard deadline.");
        }

        return remaining < TimeSpan.FromSeconds(seconds) ? remaining : TimeSpan.FromSeconds(seconds);
    }

    private void AssertRestoredSettings()
    {
        foreach (var snapshot in settings)
        {
            Assert.IsTrue(snapshot.Matches(), "Recovery did not preserve original existence and bytes: " + snapshot.Path);
        }
    }

    private void AssertOwnedProcessesStopped()
    {
        var journal = RunFiles.Read(Path.Combine(RunRoot, "cleanup-journal.json"));
        var records = journal["LeasePublishers"]!.AsArray().Select(node => ReadIdentity(node!)).ToList();
        records.Add(ReadIdentity(journal["TestProcess"]!));
        if (journal["HostWorker"] is not null)
        {
            records.Add(ReadIdentity(journal["HostWorker"]!));
        }

        var endpointPath = Path.Combine(host.OutputRoot, "endpoint-journal.json");
        if (File.Exists(endpointPath))
        {
            records.AddRange(RunFiles.Read(endpointPath)["Processes"]!.AsArray().Select(node => ReadIdentity(node!)));
        }

        Assert.IsTrue(records.All(record => !record.IsCurrent()), "A recorded owned resource remains alive after recovery.");
    }

    private static ProcessIdentity ReadIdentity(JsonNode node) => new()
    {
        Id = node[nameof(ProcessIdentity.Id)]!.GetValue<int>(),
        ParentId = node[nameof(ProcessIdentity.ParentId)]!.GetValue<int>(),
        SessionId = node[nameof(ProcessIdentity.SessionId)]!.GetValue<int>(),
        Path = node[nameof(ProcessIdentity.Path)]!.GetValue<string>(),
        StartTimeUtc = RecoveryOutcome.ProcessTimestamp(node[nameof(ProcessIdentity.StartTimeUtc)]!),
    };

    private static string[] SettingsPaths()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys");
        string[] relative = ["settings.json", "oobe_settings.json", @"MouseWithoutBorders\settings.json"];
        return relative.Select(path => Path.Combine(root, path)).ToArray();
    }

    internal static Dictionary<string, string?> SettingsFingerprint() =>
        SettingsPaths().ToDictionary(path => path, path => File.Exists(path) ? Hash(path) : null);

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void AssertNoProductProcesses()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                Assert.IsFalse(
                    process.ProcessName == "PowerToys" ||
                    process.ProcessName.StartsWith("PowerToys.", StringComparison.OrdinalIgnoreCase),
                    "Unowned or still-active PowerToys process; recovery must not stop by name.");
            }
        }
    }

    private sealed record SettingsSnapshot(string Path, bool Existed, string? Sha256)
    {
        internal bool Matches() => File.Exists(Path) == Existed && (!Existed || Hash(Path) == Sha256);
    }

    private sealed class NetworkReadyBoundaryException : Exception
    {
    }
}
