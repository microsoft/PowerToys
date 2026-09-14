// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal static class ImagePreparation
{
    public static async Task<int> RunAsync(ExecutionRequest input)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "wxc-exec.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Build the MXC image preparation helper with MxcWithWslc=true.", executable);
        }

        var store = Path.Combine(AppContext.BaseDirectory, "WslcImages");
        Directory.CreateDirectory(store);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
            },
        };
        foreach (var argument in new[] { "--setup-wslc", "--image", input.Image, "--storage-path", store })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = Program.PumpAsync(process.StandardOutput.BaseStream, isError: false);
        var stderr = Program.PumpAsync(process.StandardError.BaseStream, isError: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(input.TimeoutSeconds));
        _ = Task.Run(() =>
        {
            Console.In.ReadLine();
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Preparation has already completed.
            }
        });
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            Program.Send(new WorkerMessage(WorkerMessage.Completed, process.ExitCode == 0 ? "Image ready." : "Image preparation failed.", process.ExitCode));
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        }
    }
}
