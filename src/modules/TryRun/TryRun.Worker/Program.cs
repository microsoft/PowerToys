// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal static class Program
{
    private static readonly object OutputLock = new();
    private static int remainingOutput = OutputBuffer.MaximumCharacters;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var unavailable = RuntimeRequirements.GetUnavailableReason();
            if (unavailable is not null)
            {
                throw new InvalidOperationException(unavailable);
            }

            var support = MxcSandbox.GetPlatformSupport();
            if (!support.IsSupported)
            {
                throw new InvalidOperationException(support.Reason ?? "MXC is unavailable on this device.");
            }

            if (args is ["--probe"])
            {
                Send(new WorkerMessage(WorkerMessage.Ready, $"MXC {MxcSandbox.NativeVersion}"));
                return 0;
            }

            if (args is ["--capabilities"])
            {
                Console.WriteLine(JsonSerializer.Serialize(BackendDiscovery.Get()));
                return 0;
            }

            var input = await RequestCodec.ReadAsync(Console.In, CancellationToken.None).ConfigureAwait(false);
            if (input.PrepareImage)
            {
                return await ImagePreparation.RunAsync(input).ConfigureAwait(false);
            }

            var backends = BackendDiscovery.Get();
            if (input.IsLinux ? !backends.LinuxAvailable : !backends.WindowsAvailable)
            {
                throw new InvalidOperationException(input.IsLinux ? backends.LinuxDetail : backends.WindowsDetail);
            }

            Send(new WorkerMessage(WorkerMessage.Output, (input.IsLinux ? backends.LinuxDetail : backends.WindowsDetail) + "\n"));
            var request = SandboxRequestFactory.Create(input);
            using var process = MxcSandbox.Spawn(request);
            if (process.Warnings.Count > 0)
            {
                process.Kill();
                throw new InvalidOperationException("MXC reported a policy warning; the run was stopped: " + string.Join("; ", process.Warnings));
            }

            process.StandardInput?.Dispose();
            var stdout = PumpAsync(process.StandardOutput, isError: false);
            var stderr = PumpAsync(process.StandardError, isError: true);

            // Console's synchronized reader can block synchronously. Keep it on a
            // background thread; EOF means the owner stopped or disconnected.
            var executionFinished = 0;
            _ = Task.Run(() => WatchConnection(process, () => Volatile.Read(ref executionFinished) != 0));
            SandboxWaitResult result;
            try
            {
                result = await process.WaitAsync().ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref executionFinished, 1);
            }

            // WSLC Wait already stops/deletes its container. ProcessContainer
            // can leave descendants after the root exits, so kill its job.
            if (!input.IsLinux)
            {
                process.Kill();
            }

            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            Send(new WorkerMessage(WorkerMessage.Completed, result.TimedOut ? "Time limit reached." : "Finished.", result.ExitCode, result.TimedOut));
            return result.TimedOut ? 124 : 0;
        }
        catch (Exception exception)
        {
            Send(new WorkerMessage(WorkerMessage.Error, exception.Message));
            Send(new WorkerMessage(WorkerMessage.Completed, "Unable to run.", -1));
            return 1;
        }
    }

    private static void WatchConnection(MxcSandboxProcess process, Func<bool> finished)
    {
        try
        {
            Console.In.ReadLine();
            if (!finished())
            {
                process.Kill();
            }
        }
        catch (ObjectDisposedException)
        {
            // Normal completion may dispose the process before the owner closes.
        }
        catch (MxcException exception)
        {
            if (!finished())
            {
                Send(new WorkerMessage(WorkerMessage.Error, "MXC could not stop the workload: " + exception.Message));
            }
        }
    }

    internal static Task PumpAsync(Stream? stream, bool isError)
    {
        return Task.Run(async () =>
        {
            if (stream is null)
            {
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[1024];
            var errors = new OutputBuffer();
            int count;
            while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                if (isError)
                {
                    errors.Append(new string(buffer, 0, count));
                }
                else
                {
                    SendOutput(new string(buffer, 0, count), isError: false);
                }
            }

            if (isError)
            {
                SendOutput(PowerShellErrorFormatter.Format(errors.ToString()), isError: true);
            }
        });
    }

    private static void SendOutput(string text, bool isError)
    {
        lock (OutputLock)
        {
            var accepted = Math.Min(text.Length, remainingOutput);
            if (accepted > 0)
            {
                Send(new WorkerMessage(isError ? WorkerMessage.Error : WorkerMessage.Output, text[..accepted]));
                remainingOutput -= accepted;
                if (remainingOutput == 0)
                {
                    Send(new WorkerMessage(WorkerMessage.Output, "\n[Output limit reached; remaining output was discarded.]\n"));
                }
            }
        }
    }

    internal static void Send(WorkerMessage message)
    {
        lock (OutputLock)
        {
            Console.WriteLine(JsonSerializer.Serialize(message));
        }
    }
}
