// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class PolicyNativeControlsTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeControlsHaveSafeDefaultsAndBackendAwareEditors(bool linux)
    {
        var policy = PolicySettings.Defaults(linux);
        Assert.IsFalse(policy.Enabled("allowDaclMutation"));
        Assert.AreEqual("Auto", policy.Get("networkEnforcement"));
        await WorkflowTests.OnDispatcherAsync(() =>
        {
            var window = new PolicyWindow(policy, linux, true);
            try
            {
                Assert.AreEqual(!linux, window.Editors["allowDaclMutation"].IsEnabled);
                Assert.AreEqual(!linux, window.Editors["networkEnforcement"].IsEnabled);
                if (!linux)
                {
                    ((CheckBox)window.Editors["allowDaclMutation"]).IsChecked = true;
                    ((ComboBox)window.Editors["networkEnforcement"]).SelectedItem = "Both";
                    var edited = window.ReadSettings();
                    Assert.IsTrue(edited.Enabled("allowDaclMutation"));
                    Assert.AreEqual("Both", edited.Get("networkEnforcement"));
                    Assert.IsFalse(policy.Enabled("allowDaclMutation"));
                    ((ComboBox)window.Editors["networkMode"]).SelectedItem = "Directional";
                    Assert.IsFalse(window.Editors["networkEnforcement"].IsEnabled);
                    Assert.ThrowsException<ArgumentException>(() => window.ReadSettings().Validate(false));
                }
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void NativeControlsRejectUnsupportedOrIneffectiveCombinations()
    {
        foreach (var key in new[] { "allowDaclMutation", "networkEnforcement" })
        {
            var linux = PolicySettings.Defaults(true);
            linux.Values[key] = key == "allowDaclMutation" ? "true" : "Firewall";
            Assert.ThrowsException<ArgumentException>(() => linux.Validate(true));
        }

        foreach (var mode in new[] { "Capabilities", "Firewall", "Both", "unknown" })
        {
            var directional = PolicySettings.Defaults(false);
            directional.Values["networkMode"] = "Directional";
            directional.Values["networkEnforcement"] = mode;
            Assert.ThrowsException<ArgumentException>(() => directional.Validate(false));
        }

        foreach (var key in new[] { "allowedHosts", "blockedHosts" })
        {
            var policy = PolicySettings.Defaults(false);
            policy.Values["allowOutbound"] = "true";
            policy.Values[key] = "example.com";
            policy.Values["networkEnforcement"] = "Capabilities";
            Assert.ThrowsException<ArgumentException>(() => policy.Validate(false));
            foreach (var mode in new[] { "Auto", "Firewall", "Both" })
            {
                policy.Values["networkEnforcement"] = mode;
                policy.Validate(false);
            }
        }
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    [DataRow("Auto", false)]
    [DataRow("Capabilities", false)]
    [DataRow("Firewall", false)]
    [DataRow("Both", false)]
    [DataRow("Auto", true)]
    [DataRow("Capabilities", true)]
    [DataRow("Firewall", true)]
    [DataRow("Both", true)]
    public async Task WorkerMapsNativeControlsBeforeRunning(string mode, bool allow)
    {
        using var session = new RunSession();
        var policy = PolicySettings.Defaults(false);
        policy.Values["allowDaclMutation"] = allow ? "true" : "false";
        policy.Values["networkEnforcement"] = mode;
        var request = new ExecutionRequest("echo unexpected>must-not-run.txt", session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsBatch,
            Policy = policy,
        };
        using var snapshot = await PolicyTests.DescribeAsync(request);
        var containment = snapshot.RootElement.GetProperty("Containment");
        Assert.AreEqual(allow, containment.GetProperty("allowDaclMutation").GetBoolean());
        if (mode == "Auto")
        {
            Assert.IsFalse(containment.TryGetProperty("network", out _));
        }
        else
        {
            Assert.AreEqual(mode.ToLowerInvariant(), containment.GetProperty("network").GetProperty("enforcementMode").GetString());
        }

        Assert.IsFalse(File.Exists(Path.Combine(session.WorkingDirectory, "must-not-run.txt")));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task LegacyRequestsAlsoForbidHostDaclMutation()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("echo hi", session.WorkingDirectory, session.TemporaryDirectory, 30);
        using var snapshot = await PolicyTests.DescribeAsync(request);
        Assert.IsFalse(snapshot.RootElement.GetProperty("Containment").GetProperty("allowDaclMutation").GetBoolean());
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WindowsBatchRunsWithHostDaclMutationForbidden()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("@echo off\r\necho no-dacl-run\r\n>result.txt echo isolated", session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsBatch,
            Policy = PolicySettings.Defaults(false),
        };
        StringAssert.Contains(await MultiBackendTests.Execute(request), "no-dacl-run");
        Assert.AreEqual("isolated", File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")).Trim());
    }
}
