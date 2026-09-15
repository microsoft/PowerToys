// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class PolicyNetworkTests
{
    [TestMethod]
    [DataRow("0.8.0-alpha", false, false)]
    [DataRow("0.8.0-alpha", true, false)]
    [DataRow("0.8.0-alpha", false, true)]
    [DataRow("0.8.0-alpha", true, true)]
    [DataRow("0.9.0-alpha", false, false)]
    [DataRow("0.9.0-alpha", true, false)]
    [DataRow("0.9.0-alpha", false, true)]
    [DataRow("0.9.0-alpha", true, true)]
    [TestCategory("MXCIntegration")]
    public async Task WildcardSelectorsAreOmittedAndAcceptedByNativeParser(string version, bool destinations, bool ports)
    {
        using var run = new RunSession();
        var policy = Directional();
        policy.Values["version"] = version;
        var rule = new PolicyNetworkRule
        {
            Destinations = destinations ? [new() { Cidr = "192.0.2.0/24" }] : [],
            Ports = ports ? [new() { Protocol = "Tcp", Port = 443 }] : [],
        };
        policy.Values["egressAllow"] = JsonSerializer.Serialize(new[] { rule });
        policy.Values["egressDeny"] = policy.Get("egressAllow");
        using var snapshot = await PolicyTests.DescribeAsync(new ExecutionRequest("unused", run.WorkingDirectory, run.TemporaryDirectory, 30) { Policy = policy });
        var network = snapshot.RootElement.GetProperty("Policy").GetProperty("network");
        foreach (var list in new[] { "allow", "deny" })
        {
            var mapped = network.GetProperty("egress").GetProperty(list)[0];
            Assert.AreEqual(destinations, mapped.TryGetProperty("to", out _));
            Assert.AreEqual(ports, mapped.TryGetProperty("ports", out _));
        }

        var nativeNetwork = new JsonObject
        {
            ["egress"] = JsonNode.Parse(network.GetProperty("egress").GetRawText()),
            ["ingress"] = JsonNode.Parse(network.GetProperty("ingress").GetRawText()),
        };
        var result = await ParseNativeAsync(nativeNetwork, version: version);
        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task NativeParserRejectsTheFormerEmptyArrayRepresentation()
    {
        var network = JsonNode.Parse("""{"egress":{"default":"deny","allow":[{"to":[],"ports":[]}]}}""")!;
        var result = await ParseNativeAsync(network);
        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(result.Output, "network.egress.allow[0].to");
    }

    [TestMethod]
    [DataRow("10.128.0.0/9", "10.255.0.0/16", true)]
    [DataRow("10.128.0.1/9", "10.255.0.0/16", false)]
    [DataRow("10.128.0.0/9", "10.0.0.0/8", false)]
    [DataRow("10.128.0.0/9", "10.0.0.0/16", false)]
    [DataRow("10.0.0.0/8", "2001:db8::/32", false)]
    [DataRow("2001:db8::/33", "2001:db8:7000::/48", true)]
    [DataRow("2001:db8::/33", "2001:db8:8000::/48", false)]
    [DataRow("2001:db8::1/32", "2001:db8:1::/48", false)]
    [TestCategory("MXCIntegration")]
    public async Task ExclusionContainmentAgreesWithNativeParser(string cidr, string excluded, bool valid)
    {
        var policy = Directional();
        policy.Values["egressDeny"] = JsonSerializer.Serialize(new[] { new PolicyNetworkRule { Destinations = [new() { Cidr = cidr, Except = [excluded] }] } });
        if (valid)
        {
            policy.Validate(false);
        }
        else
        {
            Assert.ThrowsException<ArgumentException>(() => policy.Validate(false));
        }

        var network = new JsonObject
        {
            ["egress"] = new JsonObject
            {
                ["deny"] = new JsonArray(new JsonObject { ["to"] = new JsonArray(new JsonObject { ["cidr"] = cidr, ["except"] = new JsonArray(JsonValue.Create(excluded)) }) }),
            },
        };
        var result = await ParseNativeAsync(network);
        Assert.AreEqual(valid, result.ExitCode == 0, result.Output);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:8080", "Deny", "Allow", "Allow", "", true)]
    [DataRow("https://[::1]:8443", "Deny", "Allow", "Deny", "test-peer", true)]
    [DataRow("http://proxy.example:8080", "Deny", "Allow", "Allow", "", false)]
    [DataRow("http://127.0.0.1", "Deny", "Allow", "Allow", "", false)]
    [DataRow("http://127.0.0.1:80", "Deny", "Allow", "Allow", "", false)]
    [DataRow("http://127.0.0.1:8080", "Allow", "Allow", "Allow", "", false)]
    [DataRow("http://127.0.0.1:8080", "Deny", "Deny", "Allow", "", false)]
    [DataRow("http://127.0.0.1:8080", "Deny", "Allow", "Allow", "test-peer", false)]
    [DataRow("http://127.0.0.1:8080", "Deny", "Allow", "Deny", "", false)]
    [TestCategory("MXCIntegration")]
    public async Task RuntimeProxyCombinationsAgreeWithNativeParser(string url, string outbound, string inbound, string loopback, string peer, bool valid)
    {
        var policy = Directional();
        policy.Values["networkProxy"] = url;
        policy.Values["egressDefault"] = outbound;
        policy.Values["ingressDefault"] = inbound;
        policy.Values["hostLoopback"] = loopback;
        policy.Values["allowedProxyPeer"] = peer;
        if (valid)
        {
            policy.Validate(false);
            using var run = new RunSession();
            using var snapshot = await PolicyTests.DescribeAsync(new ExecutionRequest("unused", run.WorkingDirectory, run.TemporaryDirectory, 30) { Policy = policy });
            Assert.AreEqual(url, snapshot.RootElement.GetProperty("Policy").GetProperty("network").GetProperty("runtimeConfig").GetProperty("networkProxy").GetString());
            if (peer.Length != 0)
            {
                Assert.AreEqual(peer, snapshot.RootElement.GetProperty("Containment").GetProperty("network").GetProperty("allowedProxyPeer").GetString());
            }
        }
        else
        {
            Assert.ThrowsException<ArgumentException>(() => policy.Validate(false));
        }

        var network = new JsonObject
        {
            ["egress"] = new JsonObject { ["default"] = outbound.ToLowerInvariant() },
            ["ingress"] = new JsonObject { ["default"] = inbound.ToLowerInvariant(), ["hostLoopback"] = loopback.ToLowerInvariant() },
        };
        var result = await ParseNativeAsync(network, new JsonObject { ["networkProxy"] = url }, peer.Length == 0 ? null : new JsonObject { ["allowedProxyPeer"] = peer });
        Assert.AreEqual(valid, result.ExitCode == 0, result.Output);
    }

    [TestMethod]
    public void RejectsUnusableHostRulesAndRuntimeProxyDirectRules()
    {
        var basic = PolicySettings.Defaults(false);
        basic.Values["allowedHosts"] = "example.com";
        Assert.ThrowsException<ArgumentException>(() => basic.Validate(false));
        basic.Values["allowOutbound"] = "true";
        basic.Validate(false);
        var proxy = Directional();
        proxy.Values["allowedProxyPeer"] = "test-peer";
        Assert.ThrowsException<ArgumentException>(() => proxy.Validate(false));
        proxy.Values["networkProxy"] = "http://127.0.0.1:8080";
        proxy.Values["ingressDefault"] = "Allow";
        proxy.Validate(false);
        proxy.Values["egressAllow"] = "[{}]";
        Assert.ThrowsException<ArgumentException>(() => proxy.Validate(false));
    }

    [TestMethod]
    public void NetworkPolicyFuzzSeedsCoverWildcardsExclusionsAndProxies()
    {
        var random = new Random(163);
        foreach (var selector in new[] { "wildcard", "exclusion", "proxy" })
        {
            var policy = Directional();
            if (selector == "proxy")
            {
                policy.Values["networkProxy"] = "http://127.0.0.1:8080";
                policy.Values["ingressDefault"] = "Allow";
                policy.Values["hostLoopback"] = "Allow";
            }
            else
            {
                policy.Values["egressDeny"] = selector == "wildcard" ? "[{}]" :
                    JsonSerializer.Serialize(new[] { new PolicyNetworkRule { Destinations = [new() { Cidr = "2001:db8::/32", Except = ["2001:db8:1::/48"] }] } });
            }

            var request = new ExecutionRequest("unused", "C:\\work", "C:\\temp", 30) { Policy = policy };
            var seed = Encoding.UTF8.GetBytes(RequestCodec.Serialize(request));
            RequestFuzzer.FuzzTarget(seed);
            for (var iteration = 0; iteration < 500; iteration++)
            {
                var mutation = seed.ToArray();
                mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
                RequestFuzzer.FuzzTarget(mutation);
            }
        }
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WildcardDenyRuleCanRunThroughMxc()
    {
        using var run = new RunSession();
        var policy = Directional();
        policy.Values["egressDeny"] = "[{}]";
        policy.Values["timeoutMs"] = "10000";
        var output = await MultiBackendTests.Execute(new ExecutionRequest("@echo off\necho wildcard policy executed", run.WorkingDirectory, run.TemporaryDirectory, 10)
        {
            Kind = WorkloadKind.WindowsBatch,
            Policy = policy,
        });
        StringAssert.Contains(output, "wildcard policy executed");
    }

    private static PolicySettings Directional()
    {
        var policy = PolicySettings.Defaults(false);
        policy.Values["networkMode"] = "Directional";
        return policy;
    }

    private static async Task<(int ExitCode, string Output)> ParseNativeAsync(JsonNode network, JsonNode? runtime = null, JsonNode? peer = null, string version = "0.8.0-alpha")
    {
        var helper = Path.Combine(Path.GetDirectoryName(MultiBackendTests.Worker())!, "wxc-exec.exe");
        Assert.IsTrue(File.Exists(helper), "Build the matching MXC CLI helper with MxcWithWslc=true.");
        var config = new JsonObject
        {
            ["version"] = version,
            ["containment"] = "processcontainer",
            ["process"] = new JsonObject { ["commandLine"] = "TryRun-Parser-Must-Not-Execute-" + Guid.NewGuid().ToString("N") + ".exe" },
            ["network"] = network,
        };
        if (runtime is not null)
        {
            config["runtimeConfig"] = runtime;
        }

        if (peer is not null)
        {
            config["processContainer"] = new JsonObject { ["network"] = peer };
        }

        using var process = new Process { StartInfo = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("--dry-run");
        process.StartInfo.ArgumentList.Add("--config-base64");
        process.StartInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ToJsonString())));
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return (process.ExitCode, await output + "\n" + await errors);
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
