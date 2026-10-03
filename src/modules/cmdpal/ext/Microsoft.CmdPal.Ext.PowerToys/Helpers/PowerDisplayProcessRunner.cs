// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PowerToysExtension.Helpers;

internal sealed class PowerDisplayProcessRunner : IPowerDisplayProcessRunner
{
    internal const string CliExecutableName = "PowerToys.PowerDisplay.Cli.exe";

    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(8);

    private readonly Func<string> _executablePathResolver;

    internal PowerDisplayProcessRunner()
        : this(ResolveExecutablePath)
    {
    }

    internal PowerDisplayProcessRunner(Func<string> executablePathResolver)
    {
        _executablePathResolver = executablePathResolver ?? throw new ArgumentNullException(nameof(executablePathResolver));
    }

    public async Task<PowerDisplayProcessResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var executablePath = _executablePathResolver();
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return PowerDisplayProcessResult.Failed(PowerDisplayProcessFailureKind.MissingExecutable, executablePath);
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        Task<string>? standardOutputTask = null;
        Task<string>? standardErrorTask = null;
        try
        {
            if (!process.Start())
            {
                return PowerDisplayProcessResult.Failed(PowerDisplayProcessFailureKind.StartFailure, string.Empty);
            }

            // Drain both redirected streams from the moment the process starts so neither pipe can
            // fill and block the child while the other stream is being consumed.
            // Keep draining after cancellation so a killed process cannot leave redirected pipe
            // tasks unobserved; cancellation is handled by WaitForExitAsync below.
            standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var timeoutSource = new CancellationTokenSource(ProcessTimeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            try
            {
                await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await StopAndObserveProcessAsync(process, standardOutputTask, standardErrorTask).ConfigureAwait(false);

                return PowerDisplayProcessResult.Failed(
                    cancellationToken.IsCancellationRequested
                        ? PowerDisplayProcessFailureKind.Cancelled
                        : PowerDisplayProcessFailureKind.Timeout,
                    string.Empty);
            }

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            return PowerDisplayProcessResult.Completed(process.ExitCode, standardOutput, standardError);
        }
        catch (Win32Exception ex)
        {
            await StopAndObserveProcessAsync(process, standardOutputTask, standardErrorTask).ConfigureAwait(false);
            return PowerDisplayProcessResult.Failed(PowerDisplayProcessFailureKind.StartFailure, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await StopAndObserveProcessAsync(process, standardOutputTask, standardErrorTask).ConfigureAwait(false);
            return PowerDisplayProcessResult.Failed(PowerDisplayProcessFailureKind.StartFailure, ex.Message);
        }
        catch (IOException ex)
        {
            await StopAndObserveProcessAsync(process, standardOutputTask, standardErrorTask).ConfigureAwait(false);
            return PowerDisplayProcessResult.Failed(PowerDisplayProcessFailureKind.StartFailure, ex.Message);
        }
    }

    private static string ResolveExecutablePath()
    {
        var adjacentPath = Path.Combine(AppContext.BaseDirectory, CliExecutableName);
        if (File.Exists(adjacentPath))
        {
            return adjacentPath;
        }

        var installPath = PowerToysPathResolver.GetPowerToysInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var installedPath = Path.Combine(installPath, "WinUI3Apps", CliExecutableName);
            if (File.Exists(installedPath))
            {
                return installedPath;
            }
        }

        return adjacentPath;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }

    private static async Task ObserveProcessTasksAsync(
        Process process,
        Task<string> standardOutputTask,
        Task<string> standardErrorTask)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        try
        {
            var streamsTask = Task.WhenAll(standardOutputTask, standardErrorTask);
            try
            {
                await streamsTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _ = streamsTask.ContinueWith(
                    static completedTask => _ = completedTask.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }
        }
        catch (Exception)
        {
        }
    }

    private static async Task StopAndObserveProcessAsync(
        Process process,
        Task<string>? standardOutputTask,
        Task<string>? standardErrorTask)
    {
        TryKill(process);
        if (standardOutputTask is not null && standardErrorTask is not null)
        {
            await ObserveProcessTasksAsync(process, standardOutputTask, standardErrorTask).ConfigureAwait(false);
        }
    }
}
