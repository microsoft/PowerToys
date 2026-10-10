// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class LeasePublisher : IDisposable
{
    private readonly string runId;
    private readonly string stopPath;
    private readonly string outputPath;
    private readonly Process process;
    private bool stopped;

    public LeasePublisher(
        string payloadRoot,
        string controlRoot,
        string outputRoot,
        string runId,
        EndpointChannel endpoint,
        string role,
        DateTime hardDeadlineUtc,
        Process owner)
    {
        if (role is not ("Host" or "Guest"))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        this.runId = runId;
        stopPath = Path.Combine(controlRoot, $"lease-stop-{role}.json");
        outputPath = Path.Combine(outputRoot, $"{role}-lease-publisher.json");
        var configurationPath = Path.Combine(controlRoot, $"lease-publisher-{role}.json");
        RunFiles.Write(configurationPath, new
        {
            RunId = runId,
            Role = role,
            ParentId = owner.Id,
            ParentStartTimeUtc = owner.StartTime.ToUniversalTime(),
            ParentSessionId = owner.SessionId,
            HardDeadlineUtc = hardDeadlineUtc,
            InputRoot = endpoint.InputRoot,
            InitialSequence = endpoint.LeaseSequence,
            StopPath = stopPath,
            OutputPath = outputPath,
        });
        var start = new ProcessStartInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", Path.Combine(payloadRoot, "Publish-Leases.ps1"), "-ConfigurationPath", configurationPath,
        })
        {
            start.ArgumentList.Add(argument);
        }

        process = Process.Start(start) ?? throw new InvalidOperationException("Endpoint lease publisher did not start.");
    }

    public int ProcessId => process.Id;

    public void WaitForReady() => RunFiles.Wait(
        () =>
        {
            if (!File.Exists(outputPath + ".ready"))
            {
                return false;
            }

            var state = RunFiles.Read(outputPath);
            return state["RunId"]?.GetValue<string>() == runId && state["Stage"]?.GetValue<string>() == "Published";
        },
        TimeSpan.FromSeconds(30),
        "Endpoint lease publisher did not publish its initial generation.",
        ThrowIfFailed);

    public void ThrowIfFailed()
    {
        if (process.HasExited && !stopped)
        {
            var diagnostic = File.Exists(outputPath + ".ready") ? RunFiles.Read(outputPath).ToJsonString() : "No publisher state.";
            throw new InvalidOperationException($"Endpoint lease publisher exited ({process.ExitCode}): {diagnostic}");
        }
    }

    public void Stop()
    {
        if (stopped)
        {
            return;
        }

        RunFiles.Write(stopPath, new { RunId = runId });
        if (!process.WaitForExit(10_000))
        {
            throw new TimeoutException("Endpoint lease publisher did not stop after its correlated stop request.");
        }

        stopped = true;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Endpoint lease publisher failed ({process.ExitCode}); see {outputPath}.");
        }
    }

    public void Dispose()
    {
        var errors = new List<Exception>();
        try
        {
            Stop();
        }
        catch (Exception error)
        {
            errors.Add(error);
        }

        try
        {
            if (!process.HasExited)
            {
                // The Process instance retains the handle of our exact child, not a reused PID.
                process.Kill();
                if (!process.WaitForExit(5000))
                {
                    throw new TimeoutException("The owned lease publisher remains active after termination.");
                }
            }
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
        finally
        {
            process.Dispose();
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Endpoint lease publisher cleanup failed.", errors);
        }
    }
}
