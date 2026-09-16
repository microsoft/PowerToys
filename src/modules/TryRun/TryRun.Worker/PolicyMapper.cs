// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal static class PolicyMapper
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Apply(SandboxRequest request, ExecutionRequest input)
    {
        if (input.Policy is not { } options)
        {
            return;
        }

        options.Validate(input.IsLinux);
        var filesystem = request.Policy.Filesystem!;
        var runtime = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0");
        var tokens = new Dictionary<string, string?>
        {
            ["$work"] = input.WorkingDirectory,
            ["$temp"] = input.TemporaryDirectory,
            ["$runtime"] = input.IsLinux ? null : runtime,
            ["$app"] = input.ApplicationPath is null ? null : Path.GetDirectoryName(RuntimeFile.Resolve(input.ApplicationPath)),
            ["$fonts"] = input.IsLinux || input.Kind != WorkloadKind.WindowsApplication ? null : Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        };
        List<string> Resolve(string key)
        {
            List<string> paths = [];
            foreach (var entry in options.Lines(key, input.IsLinux))
            {
                var path = tokens.TryGetValue(entry, out var resolved) ? resolved : entry;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (key == "readonlyPaths" && entry == "$app")
                {
                    // This grant is derived from a validated installed executable.
                    // Copied inputs and user-supplied/writable grants still use
                    // the ancestor-locking workspace validator below.
                    paths.Add(RuntimeFile.ResolveInstallationDirectory(input.ApplicationPath!));
                    continue;
                }

                paths.Add(key == "deniedPaths" ? PolicyPaths.Normalize(path) : PolicyPaths.ResolveExisting(path));
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        filesystem.ReadonlyPaths = Resolve("readonlyPaths");
        filesystem.ReadwritePaths = Resolve("readwritePaths");
        filesystem.DeniedPaths = Resolve("deniedPaths");
        filesystem.ClearPolicyOnExit = input.IsLinux ? null : options.Enabled("clearPolicyOnExit");
        request.Policy.Version = options.Get("version");
        request.Policy.TimeoutMs = options.TimeoutMs;
        request.Policy.Telemetry = options.Get("version") == "0.9.0-alpha" ? new TelemetrySettings { Enabled = options.Enabled("telemetry") } : null;
        request.Policy.Network = Network(options);
        request.ContainerName = Empty(options.Get("containerName"));
        request.InheritDefaultEnvironment = options.Enabled("inheritDefaultEnvironment");
        foreach (var line in options.Lines("environment"))
        {
            var separator = line.IndexOf('=');
            var name = line[..separator];
            if (!input.IsLinux)
            {
                foreach (var existing in request.Environment!.Keys.Where(key => key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    request.Environment.Remove(existing);
                }
            }

            request.Environment![name] = line[(separator + 1)..];
        }

        if (request.Containment is ProcessContainerContainment windows)
        {
            windows.AllowDaclMutation = options.Enabled("allowDaclMutation");
            request.Policy.Ui = new UiPolicy
            {
                AllowWindows = options.Enabled("allowWindows"),
                Clipboard = Enum.Parse<ClipboardPolicy>(options.Get("clipboard")),
                AllowInputInjection = options.Enabled("allowInputInjection"),
            };
            windows.LeastPrivilege = options.Enabled("leastPrivilege");
            windows.LearningMode = options.Enabled("learningMode");
            windows.Capabilities = options.Lines("capabilities").ToList();
            windows.Ui = new ProcessContainerUiPolicy
            {
                Isolation = Enum.Parse<ProcessContainerUiIsolation>(options.Get("uiIsolation")),
                SystemSettings = Enum.Parse<ProcessContainerSystemSettings>(options.Get("systemSettings")),
                DesktopSystemControl = options.Enabled("desktopSystemControl"),
                Ime = options.Enabled("ime"),
            };
            windows.Network = options.Get("allowedProxyPeer").Length == 0 && options.Get("networkEnforcement") == "Auto" ? null : new ProcessContainerNetworkPolicy
            {
                AllowedProxyPeer = Empty(options.Get("allowedProxyPeer")),
                EnforcementMode = options.Get("networkEnforcement") == "Auto" ? null : Enum.Parse<ProcessContainerNetworkEnforcementMode>(options.Get("networkEnforcement")),
            };
        }
        else if (request.Containment is WslcContainment linux)
        {
            linux.CpuCount = options.Number("cpuCount") is var cpu && cpu != 0 ? cpu : null;
            linux.MemoryMb = options.Number("memoryMb") is var memory && memory != 0 ? memory : null;
            linux.Gpu = options.Enabled("gpu");
            if (options.Get("storagePath").Length > 0)
            {
                linux.StoragePath = PolicyPaths.ResolveExisting(options.Get("storagePath"));
                if (!Directory.Exists(linux.StoragePath))
                {
                    throw new ArgumentException("Image storage must be an existing directory.");
                }
            }

            linux.PortMappings = options.Lines("portMappings").Select(PolicySettings.ParsePortMapping).Select(mapping => new WslcPortMapping(mapping.Host, mapping.Container)).ToList();
            foreach (var denied in filesystem.DeniedPaths)
            {
                if (filesystem.ReadonlyPaths.Concat(filesystem.ReadwritePaths).Any(mount => PolicyPaths.IsWithin(denied, mount)))
                {
                    throw new ArgumentException("WSLC cannot enforce a denied path nested inside a mounted directory.");
                }
            }
        }
    }

    private static NetworkPolicy Network(PolicySettings options)
    {
        if (options.Get("networkMode") == "Directional")
        {
            return new NetworkPolicy
            {
                Egress = new NetworkEgressPolicy
                {
                    Default = Enum.Parse<NetworkAction>(options.Get("egressDefault")),
                    Allow = Rules(options.Rules("egressAllow")),
                    Deny = Rules(options.Rules("egressDeny")),
                },
                Ingress = new NetworkIngressPolicy { Default = Enum.Parse<NetworkAction>(options.Get("ingressDefault")), HostLoopback = Enum.Parse<NetworkAction>(options.Get("hostLoopback")) },
                RuntimeConfig = options.Get("networkProxy").Length == 0 ? null : new NetworkRuntimeConfig { NetworkProxy = options.Get("networkProxy") },
            };
        }

        return new NetworkPolicy
        {
            AllowOutbound = options.Enabled("allowOutbound"),
            AllowLocalNetwork = options.Enabled("allowLocalNetwork"),
            AllowedHosts = options.Lines("allowedHosts").ToList(),
            BlockedHosts = options.Lines("blockedHosts").ToList(),
            Proxy = options.Get("proxyKind") switch
            {
                "Host port" => new LocalhostNetworkProxyPolicy(checked((int)options.Number("proxyPort"))),
                "URL" => new UrlNetworkProxyPolicy(options.Get("proxyUrl")),
                _ => null,
            },
        };
    }

    private static List<NetworkRulePolicy> Rules(PolicyNetworkRule[] rules) => rules.Select(rule => new NetworkRulePolicy
    {
        // MXC represents a wildcard by omission. An explicit empty array is invalid.
        To = rule.Destinations.Length == 0 ? null : rule.Destinations.Select(peer => new NetworkPeerPolicy(peer.Cidr) { Except = peer.Except.ToList() }).ToList(),
        Ports = rule.Ports.Length == 0 ? null : rule.Ports.Select(port => new NetworkPortPolicy { Protocol = Enum.Parse<NetworkProtocol>(port.Protocol), Port = port.Port, EndPort = port.EndPort }).ToList(),
    }).ToList();

    internal static string Snapshot(SandboxRequest request) => JsonSerializer.Serialize(new { request.Policy, request.Containment, request.ContainerName, request.WorkingDirectory, request.Environment, request.InheritDefaultEnvironment }, SnapshotOptions);

    private static string? Empty(string value) => value.Length == 0 ? null : value;
}
