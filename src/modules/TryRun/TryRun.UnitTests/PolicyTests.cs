// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class PolicyTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EveryOptionHasADefaultAndAnEditor(bool linux)
    {
        var policy = PolicySettings.Defaults(linux);
        policy.Validate(linux);
        Assert.AreEqual(PolicySettings.Fields.Count, policy.Values.Count);
        Assert.AreEqual(PolicySettings.Fields.Count, PolicySettings.Fields.Select(field => field.Key).Distinct().Count());
        await WorkflowTests.OnDispatcherAsync(() =>
        {
            var window = new PolicyWindow(policy, linux, true);
            try
            {
                CollectionAssert.AreEquivalent(policy.Values.Keys.ToArray(), window.Editors.Keys.ToArray());
                CollectionAssert.AreEquivalent(policy.Values.ToArray(), window.ReadSettings().Values.ToArray());
                Assert.IsFalse(window.Editors[linux ? "clipboard" : "gpu"].IsEnabled);
                Assert.IsTrue(window.Editors["readwritePaths"].IsEnabled);
                ((TextBox)window.Editors["timeoutMs"]).Text = "12345";
                ((CheckBox)window.Editors["allowOutbound"]).IsChecked = true;
                var edited = window.ReadSettings();
                Assert.AreEqual(12345U, edited.TimeoutMs);
                Assert.IsTrue(edited.Enabled("allowOutbound"));
                Assert.AreEqual("60000", policy.Get("timeoutMs"), "Dialog edits must not mutate the original configuration before Apply.");
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void PolicyProtocolRoundTripsAndRejectsUnsafeOrUnknownValues()
    {
        var policy = PolicySettings.Defaults(false);
        var request = new ExecutionRequest("echo hi", "C:\\work", "C:\\temp", 30) { Policy = policy };
        var encoded = RequestCodec.Serialize(request);
        StringAssert.Contains(encoded, "\"PolicyProtocol\":1");
        CollectionAssert.AreEquivalent(policy.Values.ToArray(), RequestCodec.Parse(encoded).Policy!.Values.ToArray());
        Assert.ThrowsException<InvalidDataException>(() => RequestCodec.Parse(JsonSerializer.Serialize(request)));
        Assert.ThrowsException<InvalidDataException>(() => RequestCodec.Parse(encoded.Replace("\"PolicyProtocol\":1", "\"PolicyProtocol\":2", StringComparison.Ordinal)));
        var legacyInterpretation = JsonSerializer.Deserialize<ExecutionRequest>(encoded)!;
        Assert.ThrowsException<ArgumentNullException>(() => legacyInterpretation.Validate(), "An older worker must reject the envelope instead of ignoring permissions.");
        foreach (var pair in new[]
        {
            ("unknown", "true"), ("allowOutbound", "yes"), ("clipboard", "unknown"),
            ("timeoutMs", "-1"), ("readwritePaths", "relative-path"), ("environment", "bad line"),
            ("capabilities", "permissiveLearningMode"), ("egressAllow", "[null]"),
            ("egressAllow", "[{\"Unrecognized\":true}]"), ("telemetry", "true"),
        })
        {
            var invalid = policy.Clone();
            invalid.Values[pair.Item1] = pair.Item2;
            Assert.IsNotNull(RecordFailure(() => invalid.Validate(false)), pair.Item1);
        }

        var wrongBackend = PolicySettings.Defaults(true);
        wrongBackend.Values["clipboard"] = "Read";
        Assert.ThrowsException<ArgumentException>(() => wrongBackend.Validate(true));
        policy.Values["timeoutMs"] = "0";
        Assert.IsNull(RequestCodec.Parse(RequestCodec.Serialize(request)).EffectiveTimeoutMs);
    }

    [TestMethod]
    public void NetworkRulesKeepAllDestinationsExclusionsAndPortRanges()
    {
        var policy = PolicySettings.Defaults(false);
        policy.Values["networkMode"] = "Directional";
        var rule = new PolicyNetworkRule
        {
            Destinations = [new() { Cidr = "10.0.0.0/8", Except = ["10.1.0.0/16"] }, new() { Cidr = "2001:db8::/32" }],
            Ports = [new() { Protocol = "Tcp", Port = 443 }, new() { Protocol = "Udp", Port = 8000, EndPort = 8010 }],
        };
        policy.Values["egressAllow"] = JsonSerializer.Serialize(new[] { rule });
        policy.Validate(false);
        var parsed = policy.Rules("egressAllow").Single();
        Assert.AreEqual(2, parsed.Destinations.Length);
        Assert.AreEqual("10.1.0.0/16", parsed.Destinations[0].Except.Single());
        Assert.AreEqual((ushort)8010, parsed.Ports[1].EndPort);
        policy.Values["allowOutbound"] = "true";
        Assert.ThrowsException<ArgumentException>(() => policy.Validate(false), "Inactive basic grants must not be silently ignored.");
    }

    [TestMethod]
    public void PermissiveCaptureNeverReportsBlockedAccess()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            denials = new[] { new { resource = "fixture", resourceType = "file", accessType = "read" } },
            summary = new { totalDenials = 1, deniedResourcesTruncated = false },
        });
        var report = IsolationReportParser.ReadDenials(bytes, permissive: true);
        Assert.AreEqual("Allowed (recorded)", report.Events.Single().Outcome);
        Assert.IsFalse(report.Events.Any(row => row.Outcome == "Blocked"));
    }

    [TestMethod]
    public void PolicyProtocolFuzzMutationsStayWithinValidation()
    {
        var random = new Random(137);
        foreach (var linux in new[] { false, true })
        {
            var policy = PolicySettings.Defaults(linux);
            var request = new ExecutionRequest("echo hi", "C:\\work", "C:\\temp", 30) { Kind = linux ? WorkloadKind.LinuxShell : WorkloadKind.WindowsPowerShell, Policy = policy };
            var bytes = Encoding.UTF8.GetBytes(RequestCodec.Serialize(request));
            RequestFuzzer.FuzzTarget(bytes);
            for (var index = 0; index < 1000; index++)
            {
                var mutation = bytes.ToArray();
                mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
                RequestFuzzer.FuzzTarget(mutation);
            }
        }

        foreach (var json in new[] { "null", "[]", "{\"PolicyProtocol\":null}", "{\"PolicyProtocol\":1}", "{\"PolicyProtocol\":1,\"Request\":[]}" })
        {
            RequestFuzzer.FuzzTarget(Encoding.UTF8.GetBytes(json));
        }
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WorkerMapsEveryWindowsPolicyGroupWithoutExecutingTheWorkload()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var policy = PolicySettings.Defaults(false);
        policy.Values["version"] = "0.9.0-alpha";
        policy.Values["telemetry"] = "true";
        policy.Values["inheritDefaultEnvironment"] = "true";
        policy.Values["environment"] = "POLICY_TEST=override\nPOLICY_SPACE= value ";
        policy.Values["containerName"] = "tryrun-policy-preview";
        policy.Values["readonlyPaths"] = source.WorkingDirectory;
        policy.Values["readwritePaths"] = "$work\n$temp";
        policy.Values["deniedPaths"] = Path.Combine(source.TemporaryDirectory, "not-mounted");
        policy.Values["clearPolicyOnExit"] = "false";
        policy.Values["timeoutMs"] = "12345";
        policy.Values["allowWindows"] = "false";
        policy.Values["clipboard"] = "Read";
        policy.Values["allowInputInjection"] = "true";
        policy.Values["uiIsolation"] = "Handles";
        policy.Values["systemSettings"] = "Display";
        policy.Values["desktopSystemControl"] = "true";
        policy.Values["ime"] = "true";
        policy.Values["leastPrivilege"] = "true";
        policy.Values["learningMode"] = "true";
        policy.Values["capabilities"] = "internetClient";
        policy.Values["networkMode"] = "Directional";
        policy.Values["egressDefault"] = "Allow";
        policy.Values["ingressDefault"] = "Allow";
        policy.Values["hostLoopback"] = "Allow";
        policy.Values["networkProxy"] = "http://127.0.0.1:8080";
        policy.Values["allowedProxyPeer"] = "test-peer";
        policy.Values["egressDeny"] = JsonSerializer.Serialize(new[] { new PolicyNetworkRule { Destinations = [new() { Cidr = "10.0.0.0/8", Except = ["10.1.0.0/16"] }], Ports = [new() { Protocol = "Tcp", Port = 80, EndPort = 90 }] } });
        var request = new ExecutionRequest("Set-Content must-not-run.txt bad", run.WorkingDirectory, run.TemporaryDirectory, 30) { Policy = policy };
        using var snapshot = await DescribeAsync(request);
        var root = snapshot.RootElement;
        var configured = root.GetProperty("Policy");
        Assert.AreEqual("0.9.0-alpha", configured.GetProperty("version").GetString());
        Assert.IsTrue(configured.GetProperty("telemetry").GetProperty("enabled").GetBoolean());
        Assert.AreEqual(12345U, configured.GetProperty("timeoutMs").GetUInt32());
        Assert.IsFalse(configured.GetProperty("filesystem").GetProperty("clearPolicyOnExit").GetBoolean());
        Assert.AreEqual(source.WorkingDirectory, configured.GetProperty("filesystem").GetProperty("readonlyPaths")[0].GetString());
        Assert.AreEqual("read", configured.GetProperty("ui").GetProperty("clipboard").GetString());
        Assert.IsTrue(configured.GetProperty("ui").GetProperty("allowInputInjection").GetBoolean());
        Assert.IsFalse(configured.GetProperty("ui").GetProperty("allowWindows").GetBoolean());
        var containment = root.GetProperty("Containment");
        Assert.IsTrue(containment.GetProperty("leastPrivilege").GetBoolean());
        Assert.IsTrue(containment.GetProperty("learningMode").GetBoolean());
        Assert.AreEqual("internetClient", containment.GetProperty("capabilities")[0].GetString());
        var ui = containment.GetProperty("ui");
        Assert.AreEqual("handles", ui.GetProperty("isolation").GetString());
        Assert.AreEqual("display", ui.GetProperty("systemSettings").GetString());
        Assert.IsTrue(ui.GetProperty("desktopSystemControl").GetBoolean());
        Assert.IsTrue(ui.GetProperty("ime").GetBoolean());
        Assert.AreEqual("test-peer", containment.GetProperty("network").GetProperty("allowedProxyPeer").GetString());
        var network = configured.GetProperty("network");
        Assert.AreEqual("allow", network.GetProperty("egress").GetProperty("default").GetString());
        Assert.AreEqual(90, network.GetProperty("egress").GetProperty("deny")[0].GetProperty("ports")[0].GetProperty("endPort").GetInt32());
        Assert.AreEqual("10.1.0.0/16", network.GetProperty("egress").GetProperty("deny")[0].GetProperty("to")[0].GetProperty("except")[0].GetString());
        Assert.AreEqual("allow", network.GetProperty("ingress").GetProperty("default").GetString());
        Assert.AreEqual("allow", network.GetProperty("ingress").GetProperty("hostLoopback").GetString());
        Assert.AreEqual("http://127.0.0.1:8080", network.GetProperty("runtimeConfig").GetProperty("networkProxy").GetString());
        Assert.AreEqual("override", root.GetProperty("Environment").GetProperty("POLICY_TEST").GetString());
        Assert.AreEqual(" value ", root.GetProperty("Environment").GetProperty("POLICY_SPACE").GetString());
        Assert.IsTrue(root.GetProperty("InheritDefaultEnvironment").GetBoolean());
        Assert.AreEqual("tryrun-policy-preview", root.GetProperty("ContainerName").GetString());
        Assert.IsFalse(File.Exists(Path.Combine(run.WorkingDirectory, "must-not-run.txt")));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WorkerMapsLinuxResourcesAndBasicNetworkConfiguration()
    {
        using var run = new RunSession();
        var policy = PolicySettings.Defaults(true);
        policy.Values["cpuCount"] = "3";
        policy.Values["memoryMb"] = "3072";
        policy.Values["gpu"] = "true";
        policy.Values["storagePath"] = run.TemporaryDirectory;
        policy.Values["allowOutbound"] = "true";
        policy.Values["portMappings"] = "18765:8000\n18766:8001";
        policy.Values["proxyKind"] = "URL";
        policy.Values["proxyUrl"] = "http://proxy.example:8080";
        var request = new ExecutionRequest("echo hello", run.WorkingDirectory, run.TemporaryDirectory, 30) { Kind = WorkloadKind.LinuxShell, Policy = policy };
        using var snapshot = await DescribeAsync(request);
        var root = snapshot.RootElement;
        var containment = root.GetProperty("Containment");
        Assert.AreEqual(3, containment.GetProperty("cpuCount").GetInt32());
        Assert.AreEqual(3072, containment.GetProperty("memoryMb").GetInt32());
        Assert.IsTrue(containment.GetProperty("gpu").GetBoolean());
        Assert.AreEqual(run.TemporaryDirectory, containment.GetProperty("storagePath").GetString());
        Assert.AreEqual(18766, containment.GetProperty("portMappings")[1].GetProperty("windowsPort").GetInt32());
        Assert.AreEqual(8001, containment.GetProperty("portMappings")[1].GetProperty("containerPort").GetInt32());
        var configured = root.GetProperty("Policy");
        Assert.IsFalse(configured.TryGetProperty("ui", out _));
        Assert.IsTrue(configured.GetProperty("network").GetProperty("allowOutbound").GetBoolean());
        Assert.AreEqual("http://proxy.example:8080", configured.GetProperty("network").GetProperty("proxy").GetProperty("url").GetString());
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task CustomReadOnlyGrantAndEnvironmentReachTheWindowsSandbox()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var file = Path.Combine(source.WorkingDirectory, "granted.txt");
        File.WriteAllText(file, "read-only policy input");
        var policy = PolicySettings.Defaults(false);
        policy.Values["readonlyPaths"] += "\n" + source.WorkingDirectory;
        policy.Values["environment"] = "POLICY_TEST=configured\nPOLICY_INPUT=" + file;
        var script = "Write-Output $env:POLICY_TEST; Get-Content -LiteralPath $env:POLICY_INPUT; try { Set-Content -LiteralPath $env:POLICY_INPUT changed -ErrorAction Stop; Write-Output 'unexpected write' } catch { Write-Output 'write blocked' }";
        var output = await MultiBackendTests.Execute(new ExecutionRequest(script, run.WorkingDirectory, run.TemporaryDirectory, 30) { Policy = policy });
        StringAssert.Contains(output, "configured");
        StringAssert.Contains(output, "read-only policy input");
        StringAssert.Contains(output, "write blocked");
        Assert.IsFalse(output.Contains("unexpected write", StringComparison.Ordinal));
        Assert.AreEqual("read-only policy input", File.ReadAllText(file));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task PolicyTimeLimitControlsTheWorker()
    {
        using var run = new RunSession();
        var policy = PolicySettings.Defaults(false);
        policy.Values["timeoutMs"] = "1500";
        var result = await new WorkerClient(MultiBackendTests.Worker()).RunAsync(new ExecutionRequest("Start-Sleep -Seconds 20", run.WorkingDirectory, run.TemporaryDirectory, 60) { Policy = policy }, new MultiBackendTests.ImmediateProgress(_ => { }), CancellationToken.None);
        Assert.IsTrue(result.TimedOut);
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task CaptureAndBasicNetworkOptionsReachTheWorker()
    {
        using var run = new RunSession();
        var policy = PolicySettings.Defaults(false);
        policy.Values["allowOutbound"] = "true";
        policy.Values["allowLocalNetwork"] = "true";
        policy.Values["allowedHosts"] = "example.com";
        policy.Values["blockedHosts"] = "blocked.example.com";
        policy.Values["proxyKind"] = "Host port";
        policy.Values["proxyPort"] = "8081";
        var request = new ExecutionRequest("echo hi", run.WorkingDirectory, run.TemporaryDirectory, 30) { Policy = policy };
        using var snapshot = await DescribeAsync(request);
        var network = snapshot.RootElement.GetProperty("Policy").GetProperty("network");
        Assert.IsTrue(network.GetProperty("allowOutbound").GetBoolean());
        Assert.IsTrue(network.GetProperty("allowLocalNetwork").GetBoolean());
        Assert.AreEqual("example.com", network.GetProperty("allowedHosts")[0].GetString());
        Assert.AreEqual("blocked.example.com", network.GetProperty("blockedHosts")[0].GetString());
        Assert.AreEqual(8081, network.GetProperty("proxy").GetProperty("localhost").GetInt32());
        policy = PolicySettings.Defaults(false);
        policy.Values["captureEnabled"] = "true";
        policy.Values["captureMode"] = "Allow";
        policy.Values["retainEtl"] = "true";
        policy.Values["captureOutputPath"] = Path.Combine(run.TemporaryDirectory, "custom-capture.json");
        using var captureSnapshot = await DescribeAsync(request with { Policy = policy });
        var capture = captureSnapshot.RootElement.GetProperty("Containment").GetProperty("captureDenials");
        Assert.AreEqual("allow", capture.GetProperty("mode").GetString());
        Assert.IsTrue(capture.GetProperty("retainEtl").GetBoolean());
        Assert.AreEqual(policy.Get("captureOutputPath"), capture.GetProperty("outputPath").GetString());
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task CustomLinuxReadOnlyMountAndEnvironmentReachTheContainer()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var file = Path.Combine(source.WorkingDirectory, "granted.txt");
        File.WriteAllText(file, "linux read-only policy input");
        var policy = PolicySettings.Defaults(true);
        policy.Values["readonlyPaths"] = source.WorkingDirectory;
        policy.Values["environment"] = "POLICY_TEST=linux-configured";
        policy.Values["cpuCount"] = "3";
        policy.Values["memoryMb"] = "3072";
        var path = CommandEncoding.ShellArgument(CommandEncoding.LinuxPath(file));
        var output = await MultiBackendTests.Execute(new ExecutionRequest($"echo \"$POLICY_TEST\"; cat {path}; if echo changed > {path}; then echo 'unexpected write'; else echo 'write blocked'; fi", run.WorkingDirectory, run.TemporaryDirectory, 30) { Kind = WorkloadKind.LinuxShell, Policy = policy });
        StringAssert.Contains(output, "linux-configured");
        StringAssert.Contains(output, "linux read-only policy input");
        StringAssert.Contains(output, "write blocked");
        Assert.IsFalse(output.Contains("unexpected write", StringComparison.Ordinal));
        Assert.AreEqual("linux read-only policy input", File.ReadAllText(file));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task ExplicitPermissiveCaptureRunsAndLabelsItsReport()
    {
        using var source = new RunSession();
        using var run = new RunSession();
        var fixture = Path.Combine(source.WorkingDirectory, "fixture.txt");
        File.WriteAllText(fixture, "permissive capture fixture\n");
        var policy = PolicySettings.Defaults(false);
        policy.Values["captureEnabled"] = "true";
        policy.Values["captureMode"] = "Allow";
        IsolationReport? report = null;
        var output = new OutputBuffer();
        var request = new ExecutionRequest(string.Empty, run.WorkingDirectory, run.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            ApplicationPath = Path.Combine(Environment.SystemDirectory, "findstr.exe"),
            Arguments = ["fixture", fixture],
            Policy = policy,
        };
        var progress = new MultiBackendTests.ImmediateProgress(message =>
        {
            output.Append(message.Text);
            if (message.Report is { } value)
            {
                report = value;
            }
        });
        var result = await new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, progress, CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode, output.ToString());
        StringAssert.Contains(output.ToString(), "permissive capture fixture");
        Assert.IsNotNull(report);
        Assert.IsFalse(report.Events.Any(row => row.Source.StartsWith("MXC", StringComparison.Ordinal) && row.Outcome == "Blocked"));
        Assert.AreEqual("permissive capture fixture\n", File.ReadAllText(fixture));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task PolicyEditsPreserveBackendDraftsAndPreviousResults()
    {
        var worker = MultiBackendTests.Worker();
        await WorkflowTests.OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([], null, worker, () => true);
            try
            {
                window.Show();
                await WaitAsync(() => window.SetupStatusText.Text.StartsWith("Ready.", StringComparison.Ordinal));
                window.ProfileBox.SelectedItem = window.ProfileBox.Items.Cast<ExecutionProfile>().Single(profile => profile.Kind == WorkloadKind.WindowsPowerShell);
                var policy = PolicySettings.Defaults(false);
                policy.Values["environment"] = "POLICY_TEST=first";
                window.ApplyPolicy(policy);
                window.ScriptBox.Text = "Write-Output $env:POLICY_TEST";
                window.RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.IsFalse(window.PolicyButton.IsEnabled);
                var rejectedDuringRun = policy.Clone();
                rejectedDuringRun.Values["environment"] = "POLICY_TEST=must-not-apply";
                window.ApplyPolicy(rejectedDuringRun);
                await WaitAsync(() => window.BackButton.IsEnabled);
                Assert.AreEqual("Completed", window.RunPhaseText.Text, window.OutputBox.Text);
                StringAssert.Contains(window.OutputBox.Text, "first");
                var snapshot = window.ReportEnvironmentBox.Text;
                window.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                policy.Values["environment"] = "POLICY_TEST=second";
                window.ApplyPolicy(policy);
                Assert.AreEqual(Visibility.Collapsed, window.TimeoutPanel.Visibility);
                window.ProfileBox.SelectedItem = window.ProfileBox.Items.Cast<ExecutionProfile>().Single(profile => profile.Kind == WorkloadKind.LinuxShell);
                Assert.AreEqual(string.Empty, window.CurrentPolicy().Get("environment"));
                window.ProfileBox.SelectedItem = window.ProfileBox.Items.Cast<ExecutionProfile>().Single(profile => profile.Kind == WorkloadKind.WindowsPowerShell);
                Assert.AreEqual("POLICY_TEST=second", window.CurrentPolicy().Get("environment"));
                window.ViewResultsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(snapshot, window.ReportEnvironmentBox.Text);
                StringAssert.Contains(snapshot, "\"POLICY_TEST\": \"first\"");
                window.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await window.SelectPathsAsync([Path.Combine(Environment.SystemDirectory, "winver.exe")]);
                StringAssert.Contains(window.SetupStatusText.Text, "configured run permissions");
            }
            finally
            {
                window.Close();
                await WaitAsync(() => !window.IsVisible);
            }
        });
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(45))
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(condition(), "Timed out waiting for the policy workflow.");
    }

    private static Exception? RecordFailure(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<JsonDocument> DescribeAsync(ExecutionRequest request)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(MultiBackendTests.Worker()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("--describe-policy");
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteLineAsync(RequestCodec.Serialize(request));
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
            Assert.AreEqual(0, process.ExitCode, await output + "\n" + await errors);
            return JsonDocument.Parse(await output);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
