// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerToys.TryRun.Core;

// This catalog is shared by the form, protocol validator, defaults and coverage tests.
// A missing value in an older request receives the documented default, never a grant.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PolicySettings
{
    public static IReadOnlyList<PolicyField> Fields { get; } =
    [
        new("version", "Policy version", "Execution", "0.8.0-alpha", "0.9 is the pinned MXC development schema; required for explicit telemetry or environment inheritance.", "choice", ["0.8.0-alpha", "0.9.0-alpha"]),
        new("timeoutMs", "Time limit (milliseconds)", "Execution", "60000", "0 means no time limit. Stop remains available.", "number"),
        new("telemetry", "MXC telemetry", "Execution", "false", "Off by default. Enabling still requires MXC user consent and administrative policy; Try Run does not change either.", "bool"),
        new("environment", "Additional environment variables", "Execution", string.Empty, "One NAME=value per line. These replace individual variables in the displayed Try Run environment.", "lines"),
        new("inheritDefaultEnvironment", "Inherit the backend's default environment", "Execution", "false", "Includes the host user environment on Windows instead of only Try Run's selected variables. Requires schema 0.9.", "bool"),
        new("containerName", "Container name", "Execution", string.Empty, "Empty: MXC generates a name."),
        new("readonlyPaths", "Read-only paths", "Files", "$runtime\n$app\n$fonts", "One local path per line. $app is the installed application folder; $runtime and $fonts are Windows runtime resources. Empty tokens are omitted.", "paths", LinuxDefault: string.Empty),
        new("readwritePaths", "Writable paths", "Files", "$work\n$temp", "One local path per line. $work and $temp are this run's copies and temporary files. Adding a host path allows changes to the ORIGINAL files.", "paths"),
        new("deniedPaths", "Denied paths", "Files", string.Empty, "One local path per line. WSLC cannot exclude a child of a mounted folder.", "paths"),
        new("clearPolicyOnExit", "Clear retained policy when the process exits", "Files", "true", "Inverse of MXC lifecycle.preservePolicy. Applies to retained filesystem and network policy. Disabling may leave policy state on the host.", "bool", Backend: "Windows"),
        new("networkMode", "Network configuration", "Network", "Basic", "Basic uses outbound/LAN/host rules. Directional uses separate inbound/outbound rules; inactive settings must remain at defaults.", "choice", ["Basic", "Directional"]),
        new("allowOutbound", "Allow outbound network", "Network", "false", "WSLC networking is all-or-nothing; enabling it also makes reachable local networks accessible.", "bool"),
        new("allowLocalNetwork", "Allow local network", "Network", "false", "Windows basic network policy. WSLC uses its overall network mode.", "bool", Backend: "Windows"),
        new("allowedHosts", "Allowed hosts", "Network", string.Empty, "One host per line. Requires a host-filtering backend; MXC validates the requested combination.", "lines", Backend: "Windows"),
        new("blockedHosts", "Blocked hosts", "Network", string.Empty, "One host per line. Requires a host-filtering backend.", "lines", Backend: "Windows"),
        new("proxyKind", "Proxy", "Network", "None", "Linux URL proxies are cooperative environment variables, not enforced proxy-only traffic.", "choice", ["None", "Host port", "URL"]),
        new("proxyPort", "Host proxy port", "Network", "8080", "Used with Host port. Windows only.", "number", Backend: "Windows"),
        new("proxyUrl", "Proxy URL", "Network", string.Empty, "HTTP or HTTPS URL with an explicit non-default port, for example http://proxy.example:8080. Required when Proxy is URL."),
        new("egressDefault", "Default outbound action", "Network", "Deny", "Used in Directional mode.", "choice", ["Deny", "Allow"], Backend: "Windows"),
        new("egressAllow", "Outbound allow rules", "Network", "[]", "Destination networks, exclusions, protocols and port ranges. Windows PSEC support is required.", "rules", Backend: "Windows"),
        new("egressDeny", "Outbound deny rules", "Network", "[]", "Deny rules take precedence. Empty destinations/ports mean any.", "rules", Backend: "Windows"),
        new("ingressDefault", "Default inbound action", "Network", "Deny", "Used in Directional mode.", "choice", ["Deny", "Allow"], Backend: "Windows"),
        new("hostLoopback", "Host loopback access", "Network", "Deny", "Allowing this requires Windows PSEC support.", "choice", ["Deny", "Allow"], Backend: "Windows"),
        new("networkProxy", "Runtime loopback proxy URL", "Network", string.Empty, "Directional mode: loopback HTTP/S with a non-default port. Requires outbound Deny without direct rules and inbound Allow. A proxy peer requires host loopback Deny; otherwise Allow. Windows PSEC support required.", Backend: "Windows"),
        new("allowedProxyPeer", "Authorized proxy peer", "Network", string.Empty, "Package family or AppContainer profile identity. Directional mode only.", Backend: "Windows"),
        new("allowWindows", "Allow application windows", "Windows", "true", "PowerShell also needs Windows UI initialization. Turning this off can prevent it from starting.", "bool", Backend: "Windows"),
        new("clipboard", "Clipboard access", "Windows", "None", "Read: paste host contents into the application. Write: copy application contents to the host.", "choice", ["None", "Read", "Write", "All"], Backend: "Windows"),
        new("allowInputInjection", "Allow simulated keyboard and mouse input", "Windows", "false", "Allows the application to inject input.", "bool", Backend: "Windows"),
        new("uiIsolation", "Desktop resource access", "Windows", "Desktop", "Desktop is the compatibility mode. Handles restricts window handles; Atoms isolates global atoms; Container applies both. This does not open a separate desktop.", "choice", ["Desktop", "Handles", "Atoms", "Container"], Backend: "Windows"),
        new("systemSettings", "System settings access", "Windows", "None", "Control changes to system parameters and display settings.", "choice", ["None", "Parameters", "Display", "All"], Backend: "Windows"),
        new("desktopSystemControl", "Allow desktop and session control", "Windows", "false", "Allows desktop management and session shutdown/logoff operations.", "bool", Backend: "Windows"),
        new("ime", "Allow input method access", "Windows", "false", "Windows Input Method Editor access.", "bool", Backend: "Windows"),
        new("leastPrivilege", "Least-privilege process mode", "Windows", "false", "MXC may require a different ProcessContainer tier for this option.", "bool", Backend: "Windows"),
        new("learningMode", "Deny-and-record learning mode", "Windows", "false", "AppContainer learning diagnostics. Access remains denied; do not combine with denial capture.", "bool", Backend: "Windows"),
        new("capabilities", "Additional AppContainer capabilities", "Windows", string.Empty, "One capability name per line. Reserved learning-mode capability names are rejected.", "lines", Backend: "Windows"),
        new("captureEnabled", "Capture access checks", "Diagnostics", "false", "Native capture only. Unavailable hosts reject the request. Disable capture when using least-privilege mode, legacy proxies or denied paths, which can require elevation.", "bool", Backend: "Windows"),
        new("captureMode", "Capture mode", "Diagnostics", "Block", "Block keeps restrictions. Allow records AND ALLOWS ungranted access: a permissive policy-authoring run.", "choice", ["Block", "Allow"], Backend: "Windows"),
        new("captureOutputPath", "Capture output file", "Diagnostics", "$diagnostics\\denials.json", "MXC adds a unique run identifier. $diagnostics is private to this run; an external path persists after closing.", Backend: "Windows"),
        new("retainEtl", "Retain the native ETL trace", "Diagnostics", "false", "Trace location is shown in results. A trace in the temporary run folder is removed when that run is discarded.", "bool", Backend: "Windows"),
        new("cpuCount", "Virtual CPUs", "Linux", "2", "0 uses the backend default.", "number", Backend: "Linux"),
        new("memoryMb", "Memory (MiB)", "Linux", "2048", "0 uses the backend default.", "number", Backend: "Linux"),
        new("gpu", "Enable GPU passthrough", "Linux", "false", "Requires support on the host.", "bool", Backend: "Linux"),
        new("storagePath", "Image storage directory", "Linux", string.Empty, "Empty: Try Run's dedicated image cache. This also controls Prepare image.", Backend: "Linux"),
        new("portMappings", "TCP port mappings", "Linux", string.Empty, "One host-port:container-port per line. Requires network access; exposes a listening port on the Windows host.", "lines", Backend: "Linux"),
    ];

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    public static PolicySettings Defaults(bool linux) => new() { Values = Fields.ToDictionary(field => field.Key, field => field.DefaultFor(linux), StringComparer.Ordinal) };

    public PolicySettings Clone() => new() { Values = new(Values, StringComparer.Ordinal) };

    public string Get(string key, bool linux = false) => Values.TryGetValue(key, out var value) ? value : Fields.Single(field => field.Key == key).DefaultFor(linux);

    public bool Enabled(string key) => Get(key) == "true";

    public string[] Lines(string key, bool linux = false) => Get(key, linux).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', key == "environment" ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public uint Number(string key) => uint.Parse(Get(key), CultureInfo.InvariantCulture);

    public uint? TimeoutMs => Number("timeoutMs") is var value && value != 0 ? value : null;

    public static JsonSerializerOptions RuleJsonOptions { get; } = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public PolicyNetworkRule[] Rules(string key) => JsonSerializer.Deserialize<PolicyNetworkRule[]>(Get(key), RuleJsonOptions) ?? throw new ArgumentException("Missing network rules.");

    public void Validate(bool linux)
    {
        ArgumentNullException.ThrowIfNull(Values);
        if (Values.Count > Fields.Count || Values.Any(pair => !Fields.Any(field => field.Key == pair.Key) || pair.Value is null || pair.Value.Length > 16384 || pair.Value.Contains('\0')) || Values.Sum(pair => pair.Value.Length) > 60000)
        {
            throw new ArgumentException("Unknown or oversized policy settings.");
        }

        foreach (var field in Fields)
        {
            var value = Get(field.Key, linux);
            if (!field.AppliesTo(linux) && value != field.DefaultFor(linux))
            {
                throw new ArgumentException($"{field.Title} is not supported by the selected backend.");
            }

            if ((field.Kind == "bool" && value is not "true" and not "false") ||
                (field.Kind == "choice" && !field.Choices!.Contains(value, StringComparer.Ordinal)) ||
                (field.Kind == "number" && !uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                throw new ArgumentException($"Choose a valid value for {field.Title}.");
            }

            if (field.Kind is "lines" or "paths" && Lines(field.Key, linux).Length > 128)
            {
                throw new ArgumentException($"{field.Title}: choose at most 128 entries.");
            }

            if (field.Kind == "paths")
            {
                foreach (var path in Lines(field.Key, linux))
                {
                    if (path is not "$work" and not "$temp" and not "$runtime" and not "$app" and not "$fonts")
                    {
                        WorkspacePath.LocalPath(path);
                    }
                    else if (linux && path is "$runtime" or "$app" or "$fonts")
                    {
                        throw new ArgumentException("Windows runtime path tokens cannot be used in Linux policies.");
                    }
                }
            }
        }

        if (Get("version") != "0.9.0-alpha" && (Enabled("telemetry") || Enabled("inheritDefaultEnvironment")))
        {
            throw new ArgumentException("Telemetry and default-environment inheritance require policy version 0.9.0-alpha.");
        }

        if (linux && (Get("networkMode") != "Basic" || Get("proxyKind") == "Host port"))
        {
            throw new ArgumentException("WSLC supports basic all-or-nothing networking and URL proxies only.");
        }

        var inactive = Get("networkMode") == "Basic"
            ? new[] { "egressDefault", "egressAllow", "egressDeny", "ingressDefault", "hostLoopback", "networkProxy", "allowedProxyPeer" }
            : ["allowOutbound", "allowLocalNetwork", "allowedHosts", "blockedHosts", "proxyKind", "proxyUrl"];
        foreach (var key in inactive)
        {
            if (Get(key, linux) != Fields.Single(field => field.Key == key).DefaultFor(linux))
            {
                throw new ArgumentException($"{key} is inactive in {Get("networkMode")} network mode. Restore its default or change the mode.");
            }
        }

        if (Get("proxyKind") == "Host port" && Number("proxyPort") is < 1 or > 65535)
        {
            throw new ArgumentException("Choose a proxy port between 1 and 65535.");
        }

        if (Get("proxyKind") != "Host port" && Get("proxyPort") != "8080")
        {
            throw new ArgumentException("Restore the host proxy port default or choose Host port proxy mode.");
        }

        if (Get("proxyKind") == "URL")
        {
            PolicyNetworkValidation.ValidateUrl(Get("proxyUrl"));
        }
        else if (Get("proxyUrl").Length != 0)
        {
            throw new ArgumentException("Select URL proxy mode to use the proxy URL.");
        }

        if (Get("networkProxy").Length > 0)
        {
            PolicyNetworkValidation.ValidateUrl(Get("networkProxy"));
        }

        if (Enabled("captureEnabled") && Enabled("learningMode"))
        {
            throw new ArgumentException("Choose denial capture or deny-and-record learning mode, not both.");
        }

        if (!Enabled("captureEnabled") && (Get("captureMode") != "Block" || Enabled("retainEtl") || Get("captureOutputPath") != "$diagnostics\\denials.json"))
        {
            throw new ArgumentException("Enable access capture before changing its mode, output or trace retention.");
        }

        if (Lines("capabilities").Any(name => name.Equals("learningModeLogging", StringComparison.OrdinalIgnoreCase) || name.Equals("permissiveLearningMode", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Use the named learning/capture controls instead of reserved capabilities.");
        }

        foreach (var line in Lines("environment"))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || line[..separator].Any(character => char.IsControl(character) || character == '='))
            {
                throw new ArgumentException("Environment variables use NAME=value, one per line.");
            }
        }

        if (Lines("environment").Select(line => line[..line.IndexOf('=')]).Distinct(linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).Count() != Lines("environment").Length)
        {
            throw new ArgumentException("Environment variable names must be unique.");
        }

        foreach (var path in new[] { Get("storagePath"), Get("captureOutputPath").Replace("$diagnostics\\", "C:\\TryRunDiagnostics\\", StringComparison.Ordinal) }.Where(path => path.Length != 0))
        {
            WorkspacePath.LocalPath(path);
        }

        var mappings = Lines("portMappings").Select(ParsePortMapping).ToArray();
        if (mappings.Select(mapping => mapping.Host).Distinct().Count() != mappings.Length)
        {
            throw new ArgumentException("Host ports must be unique.");
        }

        if (mappings.Length != 0 && !Enabled("allowOutbound"))
        {
            throw new ArgumentException("WSLC port mappings require network access.");
        }

        foreach (var key in new[] { "egressAllow", "egressDeny" })
        {
            var rules = Rules(key);
            if (rules.Length > 64)
            {
                throw new ArgumentException("Choose at most 64 rules per list.");
            }

            foreach (var rule in rules)
            {
                ArgumentNullException.ThrowIfNull(rule);
                if (rule.Destinations is null || rule.Ports is null || rule.Destinations.Length > 64 || rule.Ports.Length > 64)
                {
                    throw new ArgumentException("Network rules contain at most 64 destinations and 64 port selectors.");
                }

                foreach (var peer in rule.Destinations)
                {
                    ArgumentNullException.ThrowIfNull(peer);
                    PolicyNetworkValidation.ValidatePeer(peer);
                }

                foreach (var port in rule.Ports)
                {
                    ArgumentNullException.ThrowIfNull(port);
                    if (port.Protocol is not "Any" and not "Tcp" and not "Udp" and not "Icmp" ||
                        port.Port is 0 || port.EndPort is 0 || (port.EndPort is not null && (port.Port is null || port.EndPort < port.Port)) ||
                        (port.Protocol == "Icmp" && (port.Port is not null || port.EndPort is not null)))
                    {
                        throw new ArgumentException("Choose a valid protocol and port range.");
                    }
                }
            }
        }

        PolicyNetworkValidation.ValidateProxyAndHosts(this, linux);
    }

    public static (ushort Host, ushort Container) ParsePortMapping(string line)
    {
        var parts = line.Split(':');
        if (parts.Length != 2 || !ushort.TryParse(parts[0], out var host) || !ushort.TryParse(parts[1], out var container) || host == 0 || container == 0)
        {
            throw new ArgumentException("Port mappings use host-port:container-port, with ports between 1 and 65535.");
        }

        return (host, container);
    }

    public string Describe(bool linux) => string.Join("\n", Fields.Where(field => field.AppliesTo(linux)).Select(field => $"{field.Title}: {(Get(field.Key, linux).Length == 0 ? "(none)" : Get(field.Key, linux))}"));

    public string DescribeNetwork(bool linux) => linux
        ? $"WSLC network: {(Enabled("allowOutbound") ? "on (includes reachable local networks)" : "off")}; proxy: {Get("proxyKind")}"
        : Get("networkMode") == "Directional"
            ? $"Directional; outbound {Get("egressDefault")}, inbound {Get("ingressDefault")}; host loopback {Get("hostLoopback")}"
            : $"Outbound: {(Enabled("allowOutbound") ? "on" : "off")}; local network permission: {(Enabled("allowLocalNetwork") ? "on" : "off")}; proxy: {Get("proxyKind")}; host rules: {Lines("allowedHosts").Length} allow / {Lines("blockedHosts").Length} block";
}
