// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;

namespace PowerToys.TryRun.Core;

public sealed class WorkerClient(string executablePath)
{
    public async Task<BackendAvailability> GetBackendsAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            },
        };
        process.StartInfo.ArgumentList.Add("--capabilities");
        process.Start();
        var output = new OutputBuffer();
        var errors = new OutputBuffer();
        var readOutput = DrainErrorsAsync(process.StandardOutput, output);
        var readErrors = DrainErrorsAsync(process.StandardError, errors);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await Task.WhenAll(readOutput, readErrors).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"MXC capability discovery failed. {errors} {output}");
            }

            return JsonSerializer.Deserialize<BackendAvailability>(output.ToString()) ?? throw new InvalidDataException("Missing backend information.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await Task.WhenAll(readOutput, readErrors).ConfigureAwait(false);
        }
    }

    public async Task<string?> GetAvailabilityFailureAsync(CancellationToken cancellationToken)
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("Set-Content -LiteralPath .try-run-probe.txt -Value ready -NoNewline -ErrorAction Stop", session.WorkingDirectory, session.TemporaryDirectory, 15);
        var result = await RunAsync(request, new Progress<WorkerMessage>(), cancellationToken).ConfigureAwait(false);
        var marker = Path.Combine(session.WorkingDirectory, ".try-run-probe.txt");
        if (result.ExitCode != 0 || !File.Exists(marker) || await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false) != "ready")
        {
            return "Restricted workspace access is unavailable on this device. MXC can start a process but could not write to its permitted test workspace. No user script was run.";
        }

        return null;
    }

    public async Task<WorkerMessage> RunAsync(ExecutionRequest request, IProgress<WorkerMessage> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The MXC execution worker has not been built. See the Try Run build instructions.", executablePath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            },
        };

        process.Start();
        var errors = new OutputBuffer();
        var errorReader = DrainErrorsAsync(process.StandardError, errors);
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request)).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds + 30));
            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);
            using var registration = cancellationToken.Register(() =>
            {
                RequestStop(process);

                // Allow a stopped worker to finish its bounded diagnostics and
                // send its report before tearing down the reader.
                waitCancellation.CancelAfter(TimeSpan.FromSeconds(5));
            });
            WorkerMessage? completion = null;
            while (await process.StandardOutput.ReadLineAsync(waitCancellation.Token).ConfigureAwait(false) is { } line)
            {
                var message = JsonSerializer.Deserialize<WorkerMessage>(line) ?? throw new InvalidDataException("The worker returned an empty message.");
                progress.Report(message);
                if (message.Kind == WorkerMessage.Completed)
                {
                    completion = message;
                }
            }

            await process.WaitForExitAsync(waitCancellation.Token).ConfigureAwait(false);
            await errorReader.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return completion ?? throw new InvalidOperationException($"The execution worker stopped unexpectedly. {errors}");
        }
        finally
        {
            RequestStop(process);
            using var gracePeriod = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(gracePeriod.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await errorReader.ConfigureAwait(false);
        }
    }

    private static void RequestStop(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // A worker which already exited has closed its input pipe.
        }
    }

    private static async Task DrainErrorsAsync(StreamReader reader, OutputBuffer buffer)
    {
        var characters = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(characters).ConfigureAwait(false)) != 0)
        {
            buffer.Append(new string(characters, 0, count));
        }
    }
}
