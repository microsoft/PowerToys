// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UiButton = Microsoft.PowerToys.UITest.Next.Button;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class TwoEndpointFixture : IDisposable
{
    private readonly TestContext context;
    private readonly ProcessIdentity testProcess = ProcessIdentity.Capture(Environment.ProcessId);
    private readonly List<object> phases = [];
    private readonly List<Exception> cleanupErrors = [];
    private readonly string markerPath = Environment.GetEnvironmentVariable("POWERTOYS_MWB_PROVISIONING")
        ?? @"C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json";

    private readonly string payloadRoot = Path.Combine(AppContext.BaseDirectory, "Payload");
    private readonly object journalLock = new();
    private string runId = string.Empty;
    private string productRoot = string.Empty;
    private string runRoot = string.Empty;
    private string controlRoot = string.Empty;
    private string phase = "Preflight";
    private string status = "Running";
    private string userSid = string.Empty;
    private string hostName = string.Empty;
    private string guestName = string.Empty;
    private string guestAddress = string.Empty;
    private string guestArchive = string.Empty;
    private JsonObject provision = new();
    private EndpointChannel? host;
    private EndpointChannel? guest;
    private LegacySandbox? sandbox;
    private ProcessIdentity? hostWorker;
    private readonly ManualResetEventSlim stopLease = new(false);
    private Thread? lease;
    private volatile Exception? leaseError;
    private Session? settings;
    private long hostReceiverHwnd;
    private long guestReceiverHwnd;
    private bool cleanupComplete;
    private bool hostPeersStarted;
    private bool guestPeersStarted;

    public TwoEndpointFixture(TestContext context)
    {
        this.context = context;
    }

    public void Run()
    {
        Phase("Preflight and protected provisioning identity", Prepare);
        Phase("Legacy Sandbox and correlated desktop readiness", StartWorkers);
        Phase("Matching Debug endpoints", StartPeers);
        Phase("Settings New key and Connect", Pair);
        Phase("Bidirectional owned MWB transport", VerifyTransport);
        Phase("Local keyboard negative control", VerifyLocalInput);
        Phase("Remote keyboard and physical mouse", VerifyRemoteInput);
        Phase("Clipboard off negative control and bidirectional on", VerifyClipboard);
        status = "AssertionsPassed";
        SaveJournal();
    }

    public IReadOnlyList<Exception> Cleanup()
    {
        if (cleanupComplete)
        {
            return cleanupErrors;
        }

        cleanupComplete = true;
        phase = "Cleanup";
        Attempt("Capture guest evidence before teardown", () =>
        {
            if (guest?.Ready == true)
            {
                guest.Request("Evidence");
            }
        });
        Attempt("Capture host evidence before teardown", () =>
        {
            if (host?.Ready == true)
            {
                host.Request("Evidence");
            }
        });

        bool hostStopped = !hostPeersStarted;
        bool guestStopped = !guestPeersStarted;
        Attempt("Stop guest peers and restore guest settings", () =>
        {
            if (guest?.Ready == true)
            {
                guest.Request("StopPeers", timeoutSeconds: 180);
            }

            guestStopped = true;
        });
        Attempt("Stop host peers and restore exact host settings", () =>
        {
            if (host?.Ready == true)
            {
                host.Request("StopPeers", timeoutSeconds: 90);
            }

            hostStopped = true;
        });
        Attempt("Finish guest worker", () =>
        {
            if (guest?.Ready == true && guestStopped && hostStopped)
            {
                guest.Request("Finish", new JsonObject { ["BothPeersStopped"] = true });
            }
        });
        Attempt("Close owned legacy Sandbox viewer", () =>
        {
            sandbox?.Stop();
            guestStopped = true;
        });
        Attempt("Restore private host clipboard only after both peers stop", () =>
        {
            if (host?.Ready == true)
            {
                if (!hostStopped || !guestStopped)
                {
                    throw new InvalidOperationException("Clipboard retained privately: both peers were not proved stopped.");
                }

                host.Request("Finish", new JsonObject { ["BothPeersStopped"] = true });
            }
        });
        Attempt("Wait for owned host worker", () =>
        {
            if (hostWorker is not null)
            {
                RunFiles.Wait(() => !hostWorker.IsCurrent(), TimeSpan.FromSeconds(15), "Host worker did not exit after cleanup.");
            }
        });
        Attempt("Stop the endpoint lease publisher", () =>
        {
            var publisher = lease;
            lease = null;
            if (publisher is not null)
            {
                stopLease.Set();
                if (!publisher.Join(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Endpoint lease publisher did not stop.");
                }
            }
        });
        if (leaseError is not null)
        {
            cleanupErrors.Add(leaseError);
        }

        status = cleanupErrors.Count == 0 ? "Cleaned" : "RecoveryRequired";
        Attempt("Persist cleanup journal", SaveJournal);
        if (runRoot.Length > 0)
        {
            // Profile backups are recovery data, not test attachments. Do not export them.
            foreach (var directory in new[] { runRoot, host?.OutputRoot, guest?.OutputRoot }.Where(path => path is not null))
            {
                foreach (var file in Directory.EnumerateFiles(directory!, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => Path.GetExtension(path) is ".json" or ".png"))
                {
                    Attempt("Attach " + Path.GetFileName(file), () => context.AddResultFile(file));
                }
            }
        }

        status = cleanupErrors.Count == 0 ? "Cleaned" : "RecoveryRequired";
        Attempt("Persist final cleanup outcome", SaveJournal);
        return cleanupErrors;
    }

    public void Dispose()
    {
        Cleanup();
        stopLease.Dispose();
    }

    private void Prepare()
    {
        var desktop = NativeSupport.Desktop();
        Assert.IsTrue(desktop.Ready && !desktop.Elevated, "BLOCKED_INFRASTRUCTURE: an active, unlocked standard-user L1 desktop is required.");
        Assert.AreEqual(1, System.Windows.Forms.Screen.AllScreens.Length, "The first nested-Sandbox pilot requires one L1 display.");
        Assert.AreEqual(Architecture.X64, System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture, "This pilot requires x64.");
        Assert.IsTrue(File.Exists(markerPath), $"BLOCKED_INFRASTRUCTURE: privileged provisioning marker is missing: {markerPath}");
        provision = RunFiles.Read(markerPath);
        productRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("POWERTOYS_INSTALL_DIR")
            ?? throw new InvalidOperationException("Set POWERTOYS_INSTALL_DIR to the coherently staged Debug product."));
        productRoot = productRoot.TrimEnd('\\');
        using var identity = WindowsIdentity.GetCurrent();
        userSid = identity.User!.Value;
        Assert.AreEqual(userSid, provision["TestUserSid"]?.GetValue<string>(), "Provisioning belongs to another user.");
        Assert.IsTrue(string.Equals(productRoot, provision["ProductRoot"]?.GetValue<string>()?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase),
            "Provisioning ProductRoot does not match POWERTOYS_INSTALL_DIR.");
        runId = Guid.Parse(provision["RunId"]!.GetValue<string>()).ToString();
        Assert.IsTrue(!string.IsNullOrWhiteSpace(provision["RuleName"]?.GetValue<string>()), "Missing scoped firewall rule identity.");
        Assert.IsTrue(provision["Status"]?.GetValue<string>() is "WaitingForSandbox" or "Ready",
            "BLOCKED_INFRASTRUCTURE: privileged provisioning must be WaitingForSandbox or Ready before the test.");
        LegacySandbox.AssertNoneRunning();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                Assert.IsFalse(process.ProcessName == "PowerToys" || process.ProcessName.StartsWith("PowerToys.", StringComparison.OrdinalIgnoreCase),
                    "Refusing an existing PowerToys process; the baseline must be clean.");
            }
        }

        Assert.IsTrue(WinappCli.IsAvailable(), WinappCli.InstallHint);
        var tools = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH");
        Assert.IsTrue(!string.IsNullOrWhiteSpace(tools) && File.Exists(tools), "Set WINAPP_CLI_PATH to the staged standalone winapp.exe.");
        runRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("POWERTOYS_MWB_RUN_ROOT")
            ?? Path.Combine(RunFiles.PersistentResultsRoot(context.TestRunDirectory), "mwb-" + runId));
        Assert.IsFalse(Directory.Exists(runRoot) && Directory.EnumerateFileSystemEntries(runRoot).Any(), "Run directory must be new; preserve old recovery journals.");
        controlRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToysUiTestControl", runId);
        Assert.IsFalse(Directory.Exists(controlRoot), "A control directory already exists for this RunId; provision a new run.");
        Directory.CreateDirectory(runRoot);
        host = new EndpointChannel(runId, Path.Combine(controlRoot, "host-input"), Path.Combine(runRoot, "host"));
        guest = new EndpointChannel(runId, Path.Combine(controlRoot, "guest-input"), Path.Combine(runRoot, "guest"));
        hostName = MachineName(Dns.GetHostName());
        var manifest = ValidatePayload();
        guestArchive = provision["GuestArchivePath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Provisioning must stage the guest product archive.");
        using (var archive = File.OpenRead(guestArchive))
        {
            Assert.AreEqual(provision["GuestArchiveSha256"]!.GetValue<string>(),
                Convert.ToHexString(SHA256.HashData(archive)), true, "The staged guest product archive changed.");
        }
        foreach (var endpoint in new[] { host, guest })
        {
            RunFiles.Write(Path.Combine(endpoint.InputRoot, "bootstrap.json"), new
            {
                RunId = runId,
                Role = endpoint == host ? "Host" : "Guest",
                HardDeadlineUtc = DateTime.UtcNow.AddMinutes(40),
                Manifest = manifest,
                GuestArchiveSha256 = provision["GuestArchiveSha256"]!.GetValue<string>(),
            });
            endpoint.WriteLease();
        }

        RunFiles.Write(Path.Combine(runRoot, "payload-manifest.json"), manifest);
        RunFiles.Write(Path.Combine(runRoot, "host-desktop.json"), desktop);
        sandbox = new LegacySandbox(SaveJournal);
        // UI Automation and nested-VM startup can occupy thread-pool workers for
        // long periods. The liveness lease must not depend on that same pool.
        lease = new Thread(() =>
        {
            while (!stopLease.IsSet)
            {
                try
                {
                    host.WriteLease();
                    guest.WriteLease();
                    RunFiles.Write(Path.Combine(runRoot, "lease-publisher.json"), new { TimestampUtc = DateTime.UtcNow });
                }
                catch (Exception error)
                {
                    leaseError = error;
                    return;
                }

                stopLease.Wait(TimeSpan.FromSeconds(2));
            }
        })
        {
            IsBackground = true,
            Name = "MWB endpoint lease",
        };
        lease.Start();
        SaveJournal();
    }

    private object[] ValidatePayload()
    {
        string[] files =
        [
            "PowerToys.exe", "PowerToys.MouseWithoutBorders.exe", "PowerToys.MouseWithoutBorders.dll",
            "PowerToys.MouseWithoutBorders.deps.json", "PowerToys.MouseWithoutBorders.runtimeconfig.json",
            "PowerToys.MouseWithoutBordersHelper.exe", "PowerToys.MouseWithoutBordersHelper.dll",
            "PowerToys.MouseWithoutBordersHelper.deps.json", "PowerToys.MouseWithoutBordersHelper.runtimeconfig.json",
            "PowerToys.GPOWrapper.dll", "PowerToys.Interop.dll", "PowerToys.Settings.UI.Lib.dll",
            "coreclr.dll", "hostfxr.dll", "hostpolicy.dll",
            @"WinUI3Apps\PowerToys.Settings.exe", @"WinUI3Apps\PowerToys.Settings.dll",
            @"WinUI3Apps\PowerToys.Settings.UI.Lib.dll", @"WinUI3Apps\PowerToys.Settings.deps.json",
            @"WinUI3Apps\PowerToys.Settings.runtimeconfig.json",
            @"WinUI3Apps\WinUIEdit.dll",
            @"WinUI3Apps\en-US\Microsoft.ui.xaml.dll.mui",
            @"WinUI3Apps\Microsoft.DirectManipulation.dll",
            @"WinUI3Apps\Microsoft.Graphics.Display.dll",
        ];
        var hashes = files.ToDictionary(path => path, path =>
        {
            using var stream = File.OpenRead(Path.Combine(productRoot, path));
            return Convert.ToHexString(SHA256.HashData(stream));
        });
        Assert.AreEqual(hashes["PowerToys.Settings.UI.Lib.dll"], hashes[@"WinUI3Apps\PowerToys.Settings.UI.Lib.dll"],
            "Root and WinUI3Apps settings-library builds differ.");
        foreach (var path in new[] { "PowerToys.MouseWithoutBorders.dll", "PowerToys.MouseWithoutBordersHelper.dll", @"WinUI3Apps\PowerToys.Settings.dll" })
        {
            AssertDebugAssembly(Path.Combine(productRoot, path));
        }

        if (provision["Status"]?.GetValue<string>() == "Ready" ||
            provision["ExecutableSha256"] is not null || provision["LibrarySha256"] is not null)
        {
            Assert.IsNotNull(provision["ExecutableSha256"], "Provisioning must fingerprint the staged MWB executable.");
            Assert.IsNotNull(provision["LibrarySha256"], "Provisioning must fingerprint the staged MWB assembly.");
            Assert.AreEqual(provision["ExecutableSha256"]!.GetValue<string>(), hashes["PowerToys.MouseWithoutBorders.exe"], true,
                "MWB executable changed after privileged provisioning.");
            Assert.AreEqual(provision["LibrarySha256"]!.GetValue<string>(), hashes["PowerToys.MouseWithoutBorders.dll"], true,
                "MWB assembly changed after privileged provisioning.");
        }

        var assembly = File.ReadAllBytes(Path.Combine(productRoot, "PowerToys.MouseWithoutBorders.dll"));
        Assert.IsTrue(Encoding.Unicode.GetString(assembly).Contains("POWERTOYS_MWB_ALLOW_NONCONSOLE", StringComparison.Ordinal) ||
            Encoding.Unicode.GetString(assembly, 1, assembly.Length - 1).Contains("POWERTOYS_MWB_ALLOW_NONCONSOLE", StringComparison.Ordinal),
            "MWB Debug payload is missing the approved non-console experiment flag.");
        return hashes.Select(pair => (object)new { Path = pair.Key, Sha256 = pair.Value }).ToArray();
    }

    private void StartWorkers()
    {
        var winapp = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH")!;
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = runRoot,
        };
        start.Environment["PSModulePath"] = string.Join(
            Path.PathSeparator,
            new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\Modules"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"WindowsPowerShell\Modules"),
            });
        foreach (var argument in new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-STA", "-WindowStyle", "Hidden",
            "-File", Path.Combine(payloadRoot, "EndpointWorker.ps1"),
            "-InputRoot", host!.InputRoot, "-OutputRoot", host.OutputRoot, "-ProductRoot", productRoot, "-WinApp", winapp,
        })
        {
            start.ArgumentList.Add(argument);
        }

        host.BeginBootstrap();
        using (var process = Process.Start(start) ?? throw new InvalidOperationException("Host worker did not start."))
        {
            hostWorker = ProcessIdentity.Capture(process.Id);
        }

        SaveJournal();
        var hostReady = host.WaitForReady(() =>
        {
            Assert.IsTrue(hostWorker.IsCurrent(), "Host worker exited during bootstrap; inspect its failed.json.");
        });
        hostReceiverHwnd = hostReady["ReceiverHwnd"]!.GetValue<long>();
        guest!.BeginBootstrap();
        sandbox!.Start(Path.Combine(runRoot, "run.wsb"), guestArchive, payloadRoot, Path.GetDirectoryName(winapp)!, guest!);
        var guestReady = guest!.WaitForReady(sandbox.Discover);
        guestReceiverHwnd = guestReady["ReceiverHwnd"]!.GetValue<long>();
        guestName = MachineName(guestReady["ComputerName"]!.GetValue<string>());
        Assert.AreNotEqual(hostName, guestName, "Peer identities must be distinct.");
        sandbox.AcknowledgeGuest();
        var initialProvision = provision;
        RunFiles.Wait(
            () =>
            {
                var current = RunFiles.Read(markerPath);
                Assert.AreEqual(runId, current["RunId"]?.GetValue<string>(), "BLOCKED_INFRASTRUCTURE: provisioning RunId changed.");
                Assert.AreEqual(userSid, current["TestUserSid"]?.GetValue<string>(), "BLOCKED_INFRASTRUCTURE: provisioning user changed.");
                Assert.AreEqual(initialProvision["RuleName"]?.GetValue<string>(), current["RuleName"]?.GetValue<string>(),
                    "BLOCKED_INFRASTRUCTURE: provisioning rule identity changed.");
                Assert.IsTrue(string.Equals(productRoot, current["ProductRoot"]?.GetValue<string>()?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase),
                    "BLOCKED_INFRASTRUCTURE: provisioning product identity changed.");
                var currentStatus = current["Status"]?.GetValue<string>();
                Assert.IsTrue(currentStatus is "WaitingForSandbox" or "Ready",
                    $"BLOCKED_INFRASTRUCTURE: privileged setup reported {currentStatus}; inspect its protected marker.");
                if (currentStatus == "WaitingForSandbox")
                {
                    return false;
                }

                foreach (var hashName in new[] { "ExecutableSha256", "LibrarySha256" })
                {
                    if (initialProvision[hashName] is not null)
                    {
                        Assert.AreEqual(initialProvision[hashName]!.GetValue<string>(), current[hashName]?.GetValue<string>(), true,
                            "BLOCKED_INFRASTRUCTURE: provisioned payload identity changed while waiting for Sandbox.");
                    }
                }

                provision = current;
                return true;
            },
            TimeSpan.FromMinutes(3),
            "BLOCKED_INFRASTRUCTURE: privileged setup did not publish Ready for the Sandbox Default Switch within three minutes of guest readiness.");
        ValidatePayload();
        var subnet = provision["InnerSubnet"]!.GetValue<string>();
        var hostAddress = provision["HostAddress"]!.GetValue<string>();
        Assert.IsTrue(InSubnet(hostAddress, subnet), "Provisioned host address is outside its inner subnet.");
        var candidates = guestReady["Addresses"]!.AsArray()
            .Where(node => InSubnet(node!["IPAddress"]!.GetValue<string>(), subnet) &&
                node["IPAddress"]!.GetValue<string>() != hostAddress).ToArray();
        Assert.AreEqual(1, candidates.Length,
            $"NETWORK_CHANGED: the guest must have exactly one address in provisioned Sandbox subnet {subnet}; inspect guest ready.json and reprovision the actual Default Switch.");
        guestAddress = candidates[0]!["IPAddress"]!.GetValue<string>();
        var guestInterface = candidates[0]!["InterfaceIndex"]!.GetValue<int>();
        var gateways = guestReady["Gateways"]!.AsArray()
            .Where(node => node!["InterfaceIndex"]!.GetValue<int>() == guestInterface)
            .Select(node => node!["NextHop"]!.GetValue<string>()).Distinct().ToArray();
        Assert.AreEqual(1, gateways.Length, "NETWORK_CHANGED: the selected guest interface must have one unambiguous default gateway.");
        Assert.AreEqual(hostAddress, gateways[0],
            $"NETWORK_CHANGED: guest gateway {gateways[0]} does not match provisioned HostAddress {hostAddress}; neither MWB endpoint was started.");
        RunFiles.Write(Path.Combine(runRoot, "topology.json"), new
        {
            RunId = runId, HostName = hostName, HostAddress = hostAddress, GuestName = guestName, GuestAddress = guestAddress,
            GuestGateway = gateways[0], GuestInterfaceIndex = guestInterface,
            InnerSubnet = subnet, GuestRole = "Elevated WDAGUtilityAccount on Default desktop; not service/secure desktop",
        });
    }

    private void StartPeers()
    {
        var enabled = new JsonObject();
        var moduleSource = File.ReadAllText(Path.Combine(payloadRoot, "EnabledModules.source.txt"));
        foreach (Match match in Regex.Matches(moduleSource, """(?:\[JsonPropertyName\("(?<json>[^"]+)"\)\]\s*)?public bool (?<property>\w+)"""))
        {
            var name = match.Groups["json"].Success ? match.Groups["json"].Value : match.Groups["property"].Value;
            enabled[name] = name == "MouseWithoutBorders";
        }

        Assert.IsTrue(enabled.Count >= 30 && enabled["MouseWithoutBorders"]!.GetValue<bool>(), "Enabled-module source contract was incomplete.");
        var global = new JsonObject
        {
            ["startup"] = false, ["run_elevated"] = false, ["show_whats_new_after_updates"] = false,
            ["download_updates_automatically"] = false, ["show_new_updates_toast_notification"] = false,
            ["enable_experimentation"] = false, ["enabled"] = enabled,
        };
        hostPeersStarted = true;
        var started = host!.Request("Start", new JsonObject
        {
            ["PeerName"] = guestName, ["PeerAddress"] = guestAddress, ["GlobalSettings"] = global.DeepClone(),
            ["ExpectedLocalAddress"] = provision["HostAddress"]!.DeepClone(),
        }, timeoutSeconds: 360);
        settings = Session.FromProcess(started["SettingsProcessId"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture), timeoutMS: 30_000);
        guestPeersStarted = true;
        // The guest's WinUI3 Settings window can take longer than the host's to satisfy the
        // startup check: it is one level deeper, inside a Windows Sandbox with no GPU
        // passthrough. Give this channel call ample headroom above EndpointSupport.ps1's own
        // guest startup budget (8 minutes) so that budget - not this outer channel timeout -
        // is what fails first; run localvm-20260914-050434 observed the mapped-folder
        // response round trip alone add roughly two extra minutes on top of the internal
        // budget under this nested VM's I/O load.
        guest!.Request("Start", new JsonObject
        {
            ["PeerName"] = hostName, ["PeerAddress"] = provision["HostAddress"]!.DeepClone(), ["GlobalSettings"] = global.DeepClone(),
            ["ExpectedLocalAddress"] = guestAddress,
        }, timeoutSeconds: 900);
    }

    private void Pair()
    {
        if (!settings!.Has(By.AccessibilityId("MouseWithoutBordersNavItem"), timeoutMS: 500))
        {
            settings.Find<NavigationViewItem>(By.AccessibilityId("InputOutputNavItem")).Invoke(msPostAction: 0);
        }

        settings.Find<NavigationViewItem>(By.AccessibilityId("MouseWithoutBordersNavItem")).Invoke(msPostAction: 0);
        var buttons = settings.FindAll<UiButton>(By.Name("New key"), timeoutMS: 20_000).Where(button => button.Name == "New key").ToArray();
        Assert.AreEqual(1, buttons.Length, "Expected one real Settings New key button.");
        var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, @"MouseWithoutBorders\settings.json");
        var previous = RunFiles.Read(path)["properties"]!["SecurityKey"]!["value"]!.GetValue<string>();
        buttons[0].Invoke(msPostAction: 0);
        string key = string.Empty;
        RunFiles.Wait(() =>
        {
            key = RunFiles.Read(path)["properties"]!["SecurityKey"]!["value"]!.GetValue<string>();
            return key.Length > 0 && key != previous;
        }, TimeSpan.FromSeconds(20), "Settings New key did not change the persisted key.");
        host!.Request("VerifyPeerMapping");
        guest!.Request("Connect", new JsonObject { ["Key"] = key, ["PeerName"] = hostName }, timeoutSeconds: 600);
    }

    private void VerifyTransport()
    {
        var stable = 0;
        RunFiles.Wait(() =>
        {
            var hostTransport = host!.Request("Transport");
            var guestTransport = guest!.Request("Transport");
            RunFiles.Write(Path.Combine(runRoot, "host-transport.json"), hostTransport);
            RunFiles.Write(Path.Combine(runRoot, "guest-transport.json"), guestTransport);
            Assert.AreEqual((int)Key.F1, hostTransport["SwitchKey"]!.GetValue<int>(), "The isolated baseline must use F1-F4 machine switching.");
            bool established = HasPeer(hostTransport, guestAddress) &&
                HasPeer(guestTransport, provision["HostAddress"]!.GetValue<string>()) &&
                HasRoutingLayout(hostTransport) && HasRoutingLayout(guestTransport);
            stable = established ? stable + 1 : 0;
            return stable >= 2;
        }, TimeSpan.FromSeconds(90), "Both owned MWB PIDs must have peer transport and the expected host/guest routing slots.");
        // ItemsControl itself has no UIA peer; its bound device labels do.
        RunFiles.Wait(() => settings!.FindAll<TextBlock>(By.Name(guestName), timeoutMS: 1000)
            .Any(element => string.Equals(element.Name, guestName, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(15), "Settings devices did not display the paired guest.");
    }

    private void VerifyLocalInput()
    {
        guest!.Request("ClearInput");
        guest.Request("FocusInput");
        host!.Request("ClearInput");
        FocusHost();
        SelectMachine(Key.F1);
        TypeToken("local1357924680");
        RunFiles.Wait(() => Observe(host)["Text"]!.GetValue<string>() == "local1357924680",
            TimeSpan.FromSeconds(10), "Local control token was not received by the host.");
        var guestState = Observe(guest);
        Assert.AreEqual(string.Empty, guestState["Text"]!.GetValue<string>(), "Local control leaked to the guest.");
        Assert.AreEqual(0, guestState["Clicks"]!.GetValue<int>());
    }

    private void VerifyRemoteInput()
    {
        guest!.Request("FocusInput");
        FocusHost();
        SelectMachine(Key.F2);
        TypeToken("remote2468135790");
        RunFiles.Wait(() => Observe(guest)["Text"]!.GetValue<string>() == "remote2468135790",
            TimeSpan.FromSeconds(15), "Remote token never reached the independent guest receiver.");
        Assert.AreEqual("local1357924680", Observe(host!)["Text"]!.GetValue<string>(), "Remote token leaked into the foreground host receiver.");
        var before = Observe(guest);
        var hostClicks = Observe(host!)["Clicks"]!.GetValue<int>();
        int beforeX = before["CursorX"]!.GetValue<int>();
        int beforeY = before["CursorY"]!.GetValue<int>();
        RequireHostForeground();
        MouseHelper.MoveBy(30, 30, steps: 3);
        RunFiles.Wait(() =>
        {
            var current = Observe(guest);
            return current["CursorX"]!.GetValue<int>() != beforeX || current["CursorY"]!.GetValue<int>() != beforeY;
        }, TimeSpan.FromSeconds(10), "Real host mouse movement did not reach the guest.");
        RunFiles.Wait(() =>
        {
            var current = Observe(guest);
            int x = current["CursorX"]!.GetValue<int>();
            int y = current["CursorY"]!.GetValue<int>();
            int left = current["ClickLeft"]!.GetValue<int>();
            int right = current["ClickRight"]!.GetValue<int>();
            int top = current["ClickTop"]!.GetValue<int>();
            int bottom = current["ClickBottom"]!.GetValue<int>();
            if (x > left + 30 && x < right - 30 && y > top + 30 && y < bottom - 30)
            {
                return true;
            }

            RequireHostForeground();
            MouseHelper.MoveBy(Math.Clamp(((left + right) / 2) - x, -100, 100),
                Math.Clamp(((top + bottom) / 2) - y, -100, 100), steps: 4);
            return false;
        }, TimeSpan.FromSeconds(20), "Remote cursor did not enter the receiver's physical click surface.");
        Assert.AreEqual(guestReceiverHwnd, Observe(guest)["ForegroundHwnd"]!.GetValue<long>(), "Guest receiver lost foreground before click.");
        RequireHostForeground();
        MouseHelper.LeftClick();
        RunFiles.Wait(() => Observe(guest)["Clicks"]!.GetValue<int>() == 1, TimeSpan.FromSeconds(10), "Remote physical click did not increment the guest-only counter.");
        Assert.AreEqual(hostClicks, Observe(host!)["Clicks"]!.GetValue<int>(), "Remote click incremented the host counter.");
        SelectMachine(Key.F1);
    }

    private void VerifyClipboard()
    {
        var baseline = Publish(guest!);
        var source = Publish(host!);
        var negative = Stopwatch.StartNew();
        do
        {
            Assert.AreEqual(baseline, Observe(guest!)["ClipboardDigest"]!.GetValue<string>(), "Clipboard-sharing-off did not preserve the guest's independent baseline.");
            Assert.AreEqual(source, Observe(host!)["ClipboardDigest"]!.GetValue<string>(), "Clipboard-sharing-off changed the source.");
            Thread.Sleep(250);
        }
        while (negative.Elapsed < TimeSpan.FromSeconds(5));

        var card = settings!.Find(By.AccessibilityId("MouseWithoutBordersShareClipboard"));
        // Element.Find currently searches the whole session. Inspect this exact subtree instead:
        // the page has several unnamed ToggleSwitch controls and a TransferFile CheckBox.
        var toggles = Descendants(InspectSubtree(card)).Where(element =>
            element.TryGetProperty("className", out var className) && className.GetString() == "ToggleSwitch").ToArray();
        Assert.AreEqual(1, toggles.Length, "Expected one clipboard-sharing toggle in its Settings card.");
        var toggle = settings.Find<ToggleSwitch>(By.Slug(toggles[0].GetProperty("selector").GetString()!));
        Assert.IsFalse(toggle.IsOn, "Host clipboard sharing must begin disabled.");
        toggle.Invoke(msPostAction: 0);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", 10_000), "Host clipboard sharing UI did not enable.");
        guest!.Request("ClipboardSharing", new JsonObject { ["Enabled"] = true }, timeoutSeconds: 90);
        var hostSource = Publish(host!);
        WaitClipboard(guest, hostSource);
        var guestSource = Publish(guest);
        WaitClipboard(host!, guestSource);
    }

    private string Publish(EndpointChannel endpoint)
    {
        // MWB throttles clipboard copies, including changes received from its peer.
        Thread.Sleep(1500);
        return endpoint.Request("PublishClipboard")["Digest"]!.GetValue<string>();
    }

    private JsonElement InspectSubtree(Element element) => WinappCli.InvokeJson(
        "ui", "inspect", element.Selector, settings!.TargetFlag, settings.TargetValue, "--json", "-d", "6");

    private static IEnumerable<JsonElement> Descendants(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in Descendants(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Descendants(item))
                {
                    yield return child;
                }
            }
        }
    }

    private void WaitClipboard(EndpointChannel destination, string sourceDigest)
    {
        RunFiles.Wait(() => Observe(destination)["ClipboardDigest"]!.GetValue<string>() == sourceDigest,
            TimeSpan.FromSeconds(15), "Synthetic clipboard did not arrive over MWB.");
    }

    private JsonObject Observe(EndpointChannel endpoint)
    {
        var state = endpoint.Request("Observe");
        Assert.IsTrue(state["Desktop"]!["Ready"]!.GetValue<bool>(), "Endpoint left the active Default desktop.");
        RunFiles.Write(Path.Combine(endpoint.OutputRoot, "receiver-state.json"), state);
        return state;
    }

    private void FocusHost()
    {
        host!.Request("FocusInput");
        WindowControl.TryBringToForeground(new IntPtr(hostReceiverHwnd));
        RequireHostForeground();
    }

    private void RequireHostForeground()
    {
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(hostReceiverHwnd), timeoutMS: 5000, requiredConsecutiveMatches: 3),
            $"No input sent: exact host receiver HWND must own foreground, not Sandbox. Actual: {WindowControl.GetForegroundWindowInfo()}");
    }

    private void SelectMachine(Key machine)
    {
        RequireHostForeground();
        // MWB resets its simulated-input guard on the next mouse hook event.
        // Establish real pointer activity before a keyboard-only routing command.
        MouseHelper.MoveBy(1, 0, steps: 1);
        KeyboardHelper.PressKey(Key.Ctrl);
        KeyboardHelper.PressKey(Key.Alt);
        try
        {
            KeyboardHelper.PressKey(machine);
            Thread.Sleep(80);
        }
        finally
        {
            KeyboardHelper.ReleaseKey(machine);
            KeyboardHelper.ReleaseKey(Key.Alt);
            KeyboardHelper.ReleaseKey(Key.Ctrl);
        }

        Thread.Sleep(500);
    }

    private bool HasRoutingLayout(JsonObject transport)
    {
        var matrix = transport["MachineMatrix"]!.AsArray();
        return matrix.Count >= 2 &&
            string.Equals(matrix[0]?.GetValue<string>(), hostName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(matrix[1]?.GetValue<string>(), guestName, StringComparison.OrdinalIgnoreCase);
    }

    private void TypeToken(string token)
    {
        foreach (var character in token)
        {
            RequireHostForeground();
            var key = (Key)char.ToUpperInvariant(character);
            KeyboardHelper.PressKey(key);
            try
            {
                Thread.Sleep(50);
            }
            finally
            {
                KeyboardHelper.ReleaseKey(key);
            }

            Thread.Sleep(50);
        }
    }

    private void Phase(string name, Action action)
    {
        phase = name;
        context.WriteLine(name);
        SaveJournal();
        var started = DateTime.UtcNow;
        try
        {
            if (leaseError is not null)
            {
                throw new IOException("Endpoint lease publication failed.", leaseError);
            }

            action();
            phases.Add(new { Name = name, Status = "PASS", StartedUtc = started, CompletedUtc = DateTime.UtcNow });
        }
        catch (Exception error)
        {
            status = "Failed";
            phases.Add(new { Name = name, Status = "FAIL", StartedUtc = started, CompletedUtc = DateTime.UtcNow, Error = error.Message });
            throw;
        }
        finally
        {
            SaveJournal();
        }
    }

    private void Attempt(string description, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            cleanupErrors.Add(new InvalidOperationException(description, error));
            context.WriteLine($"Cleanup failure: {description}: {error.Message}");
        }
    }

    private void SaveJournal()
    {
        if (runRoot.Length == 0 || !Directory.Exists(runRoot))
        {
            return;
        }

        lock (journalLock)
        {
            RunFiles.Write(Path.Combine(runRoot, "cleanup-journal.json"), new
            {
                FormatVersion = 1, RunId = runId, RunRoot = runRoot, ControlRoot = controlRoot, ProductRoot = productRoot,
                HostName = hostName, TestProcess = testProcess,
                ProvisioningMarker = markerPath, RuleName = provision["RuleName"]?.GetValue<string>(),
                TestUserSid = userSid, HostWorker = hostWorker, SandboxProcesses = sandbox?.Processes,
                ViewerHwnd = sandbox?.ViewerHwnd, GuestAcknowledged = sandbox?.GuestAcknowledged,
                HostEndpointJournal = host is null ? null : Path.Combine(host.OutputRoot, "endpoint-journal.json"),
                GuestEndpointJournal = guest is null ? null : Path.Combine(guest.OutputRoot, "endpoint-journal.json"),
                Phase = phase, Status = status, TimestampUtc = DateTime.UtcNow,
                CleanupErrors = cleanupErrors.Select(error => error.ToString()).ToArray(),
                PrivilegedCleanupRequired = true,
            });
            RunFiles.Write(Path.Combine(runRoot, "phases.json"), new { RunId = runId, Phases = phases });
        }
    }

    private static bool HasPeer(JsonObject transport, string peer) =>
        transport["Connections"]!.AsArray().Any(connection =>
            IPAddress.Parse(connection!["RemoteAddress"]!.GetValue<string>()).MapToIPv4().ToString() == peer);

    private static string MachineName(string name) => name[..Math.Min(32, name.Length)].Trim();

    private static bool InSubnet(string address, string subnet)
    {
        var parts = subnet.Split('/');
        var prefix = int.Parse(parts[1], CultureInfo.InvariantCulture);
        Assert.IsTrue(prefix is >= 16 and <= 30, "The firewall subnet is broader than the approved inner-network scope.");
        var bytes = IPAddress.Parse(address).GetAddressBytes();
        var network = IPAddress.Parse(parts[0]).GetAddressBytes();
        if (bytes.Length != 4 || network.Length != 4)
        {
            return false;
        }

        for (var index = 0; index < 4; index++)
        {
            var bits = Math.Clamp(prefix - (index * 8), 0, 8);
            var mask = (255 << (8 - bits)) & 255;
            if ((bytes[index] & mask) != (network[index] & mask))
            {
                return false;
            }
        }

        return true;
    }

    private static void AssertDebugAssembly(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (metadata.GetString(type.Name) != "AssemblyConfigurationAttribute")
            {
                continue;
            }

            var value = metadata.GetBlobReader(attribute.Value);
            Assert.AreEqual((ushort)1, value.ReadUInt16(), "Invalid assembly configuration metadata.");
            Assert.AreEqual("Debug", value.ReadSerializedString(), $"Pilot rejects non-Debug payload: {Path.GetFileName(path)}");
            return;
        }

        Assert.Fail($"Debug assembly configuration metadata is missing: {Path.GetFileName(path)}");
    }
}
