// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal sealed class RunDiagnostics(ExecutionRequest input, BackendAvailability backends)
{
    private string? probeText;
    private string? probePath;

    private string? captureRoot;

    private bool CaptureRequested => input.Policy?.Enabled("captureEnabled") ?? input.CaptureDenials;

    public bool CaptureEnabled => CaptureRequested && !input.IsLinux && backends.NativeDenialCaptureAvailable;

    public bool Permissive => CaptureEnabled && input.Policy?.Get("captureMode") == "Allow";

    public void Configure(SandboxRequest request)
    {
        if (input.Policy is not null && CaptureRequested && !CaptureEnabled)
        {
            throw new InvalidOperationException("Native MXC capture is unavailable. Turn off access capture or use a compatible host.");
        }

        if (CaptureEnabled && request.Containment is ProcessContainerContainment containment)
        {
            if (containment.LeastPrivilege || request.Policy.Network?.Proxy is not null || request.Policy.Filesystem?.DeniedPaths.Count > 0)
            {
                throw new InvalidOperationException("This policy can require MXC's elevated capture fallback. Try Run uses native capture only. Disable access capture when using least-privilege mode, a legacy proxy, or denied paths.");
            }

            var path = (input.Policy?.Get("captureOutputPath") ?? "$diagnostics\\denials.json").Replace("$diagnostics\\", RunArtifacts.DiagnosticsDirectory(input) + "\\", StringComparison.Ordinal);
            captureRoot = PolicyPaths.ResolveExisting(Path.GetDirectoryName(path)!);
            if (!Directory.Exists(captureRoot))
            {
                throw new ArgumentException("Capture output requires an existing parent directory.");
            }

            containment.CaptureDenials = new CaptureDenialsPolicy
            {
                Mode = Permissive ? CaptureDenialsMode.Allow : CaptureDenialsMode.Block,
                OutputPath = Path.Combine(captureRoot, Path.GetFileName(path)),
                RetainEtl = input.Policy?.Enabled("retainEtl") == true,
            };
        }

        if (input.IsolationDemo)
        {
            probePath = Path.Combine(RunArtifacts.DiagnosticsDirectory(input), "host-only-probe.txt");
            probeText = "Harmless Try Run probe " + Guid.NewGuid().ToString("N");
            File.WriteAllText(probePath, probeText, new UTF8Encoding(false));
            if (File.ReadAllText(probePath) != probeText)
            {
                throw new IOException("The host-side demo control could not be verified.");
            }

            request.Environment!["TRYRUN_HOST_PROBE"] = input.IsLinux ? CommandEncoding.LinuxPath(probePath) : probePath;
        }
    }

    public RunEnvironment Describe(SandboxRequest request)
    {
        return new RunEnvironment(
            input.IsLinux ? "Linux / MXC WSLC" : "Windows / MXC ProcessContainer",
            input.ApplicationPath ?? input.FileRelativePath ?? "Inline script",
            input.IsLinux ? CommandEncoding.LinuxPath(Path.Combine(input.WorkingDirectory, input.WorkingSubdirectory ?? string.Empty)) : request.WorkingDirectory ?? input.WorkingDirectory,
            request.Policy.Filesystem?.ReadonlyPaths?.ToArray() ?? [],
            request.Policy.Filesystem?.ReadwritePaths?.ToArray() ?? [],
            input.Policy?.DescribeNetwork(input.IsLinux) ?? "Outbound and local-network access disabled",
            input.IsLinux ? "No desktop UI policy" : $"Windows: {request.Policy.Ui?.AllowWindows}; clipboard: {request.Policy.Ui?.Clipboard}; input injection: {request.Policy.Ui?.AllowInputInjection}",
            request.Containment is WslcContainment linux ? $"Image {linux.Image}; CPUs: {linux.CpuCount?.ToString(CultureInfo.InvariantCulture) ?? "backend default"}; Memory: {linux.MemoryMb?.ToString(CultureInfo.InvariantCulture) ?? "backend default"} MiB; GPU: {linux.Gpu}" : "No additional CPU or memory cap configured",
            input.TimeoutSeconds,
            backends.NativeVersion)
        {
            TimeLimit = request.Policy.TimeoutMs is { } timeout ? timeout + " milliseconds" : "None",
            PolicySnapshot = PolicyMapper.Snapshot(request),
        };
    }

    public IsolationReport Pending()
    {
        return CaptureEnabled
            ? new IsolationReport("Collecting", Permissive ? "PERMISSIVE capture: ungranted access is allowed and recorded. This run does not enforce deny-by-default." : "MXC native denial capture, block mode. Requests remain denied while being recorded.", [])
            : new IsolationReport(
                "Not collected",
                input.IsLinux ? "MXC WSLC does not expose Windows denial capture. See configured mounts and clearly labeled workload observations." : input.CaptureDenials ? "Native denial capture is unavailable on this host. The configured restrictions remain in effect; no elevated capture fallback is used." : "Native denial capture was not requested. Configured restrictions remain in effect.",
                []);
    }

    public IsolationReport Complete(ISandboxProcess process)
    {
        var report = Pending();
        if (CaptureEnabled)
        {
            try
            {
                var metadata = process.OutputMetadata;
                if (metadata?.CaptureDenialsError is { } error)
                {
                    throw new IOException(error.Message + (string.IsNullOrWhiteSpace(error.EtlPath) ? string.Empty : " MXC retained a trace at: " + error.EtlPath));
                }

                var capture = metadata?.CaptureDenials ?? throw new IOException("MXC did not return a denial document.");
                var bytes = RunArtifacts.ReadFile(capture.OutputPath, captureRoot!, IsolationReportParser.MaximumDocumentBytes);
                report = IsolationReportParser.ReadDenials(bytes, Permissive);
                report = report with { CaptureDetail = report.CaptureDetail + "\nCapture file: " + capture.OutputPath + (capture.EtlPath is null ? string.Empty : "\nRetained trace: " + capture.EtlPath) };
            }
            catch (Exception exception)
            {
                report = new IsolationReport("Unavailable", "The run's denial report could not be read: " + Short(exception.Message), []);
            }
        }

        var events = report.Events.ToList();
        var observations = Path.Combine(input.WorkingDirectory, input.WorkingSubdirectory ?? string.Empty, RunArtifacts.ObservationFileName);
        if (File.Exists(observations))
        {
            try
            {
                var bytes = RunArtifacts.ReadFile(observations, input.WorkingDirectory, IsolationReportParser.MaximumObservationBytes);
                events.AddRange(IsolationReportParser.ReadObservations(bytes));
            }
            catch (Exception exception)
            {
                events.Add(new IsolationEvent("Workload file (self-reported)", RunArtifacts.ObservationFileName, "Read", "Unavailable", Short(exception.Message)));
            }
        }

        if (probePath is not null)
        {
            try
            {
                var actual = Encoding.UTF8.GetString(RunArtifacts.ReadFile(probePath, RunArtifacts.DiagnosticsDirectory(input), 1024));
                events.Add(new IsolationEvent("Host verification", "Harmless host fixture", "Read / compare", actual == probeText ? "Unchanged" : "Changed", "The host verified this fixture was readable before the run and compared its contents after the run. See the submitted policy for this run's grants."));
            }
            catch (Exception exception)
            {
                events.Add(new IsolationEvent("Host verification", "Harmless host fixture", "Read / compare", "Unavailable", Short(exception.Message)));
            }
        }

        return report with { Events = events };
    }

    private static string Short(string text) => text.Length <= 1024 ? text : text[..1024];
}
