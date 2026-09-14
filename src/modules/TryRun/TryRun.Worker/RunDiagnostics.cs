// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal sealed class RunDiagnostics(ExecutionRequest input, BackendAvailability backends)
{
    private string? probeText;
    private string? probePath;

    public bool CaptureEnabled => input.CaptureDenials && !input.IsLinux && backends.NativeDenialCaptureAvailable;

    public void Configure(SandboxRequest request)
    {
        if (CaptureEnabled && request.Containment is ProcessContainerContainment containment)
        {
            containment.CaptureDenials = new CaptureDenialsPolicy
            {
                Mode = CaptureDenialsMode.Block,
                OutputPath = Path.Combine(RunArtifacts.DiagnosticsDirectory(input), "denials.json"),
                RetainEtl = false,
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
            "Outbound and local-network access disabled",
            input.IsLinux ? "No desktop UI policy" : "Windows allowed; clipboard and input injection disabled",
            input.IsLinux ? $"Image {input.Image}; 2 CPUs / 2 GiB; only Work and Temp mounted" : "No additional CPU or memory cap configured",
            input.TimeoutSeconds,
            backends.NativeVersion);
    }

    public IsolationReport Pending()
    {
        return CaptureEnabled
            ? new IsolationReport("Collecting", "MXC native denial capture, block mode. Requests remain denied while being recorded.", [])
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
                var bytes = RunArtifacts.ReadFile(capture.OutputPath, RunArtifacts.DiagnosticsDirectory(input), IsolationReportParser.MaximumDocumentBytes);
                report = IsolationReportParser.ReadDenials(bytes);
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
                events.Add(new IsolationEvent("Host verification", "Harmless file outside granted workspace", "Read / compare", actual == probeText ? "Unchanged" : "Changed", "The host verified this fixture was readable before the run and compared its contents after the run. The fixture directory was not granted to the workload."));
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
