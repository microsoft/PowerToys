// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class WinAppSandboxCommand : IDisposable
{
    private const int OutputLimit = 1024 * 1024;
    private static readonly object LaunchLock = new();
    private readonly Process process;
    private readonly CancellationTokenSource cancellation = new();
    private readonly StringBuilder stdout = new();
    private readonly StringBuilder stderr = new();
    private readonly Task stdoutReader;
    private readonly Task stderrReader;
    private bool disposed;

    private WinAppSandboxCommand(Process process)
    {
        this.process = process;
        stdoutReader = DrainAsync(process.StandardOutput, stdout);
        stderrReader = DrainAsync(process.StandardError, stderr);
    }

    public bool HasExited => disposed || process.HasExited;

    public int ProcessId => process.Id;

    public DateTime StartTimeUtc => process.StartTime.ToUniversalTime();

    public string StandardOutput
    {
        get
        {
            lock (stdout)
            {
                return stdout.ToString();
            }
        }
    }

    public static WinAppSandboxCommand Start(string executable, IEnumerable<string> arguments, string targetStateRoot, string workingDirectory)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["WINAPP_TARGET_STATE_ROOT"] = targetStateRoot;
        start.Environment["WINAPP_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["WINAPP_CLI_UPDATE_CHECK"] = "0";
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            lock (LaunchLock)
            {
                return new WinAppSandboxCommand(Process.Start(start) ?? throw new WinAppSandboxException("command_start_failed"));
            }
        }
        catch (Exception error) when (error is Win32Exception or IOException)
        {
            throw new WinAppSandboxException("command_start_failed");
        }
    }

    public static Process StartClient(string executable, IEnumerable<string> arguments, string workingDirectory, Func<ProcessStartInfo, Process?>? launch = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // The client can outlive the CLI launcher. Neither its own streams nor
        // inherited copies of the test runner's pipes may hold the runner open.
        lock (LaunchLock)
        {
            var restore = new List<nint>();
            try
            {
                foreach (var kind in new[] { -10, -11, -12 })
                {
                    var handle = GetStdHandle(kind);
                    if (handle is 0 or -1)
                    {
                        continue;
                    }

                    if (!GetHandleInformation(handle, out var flags))
                    {
                        throw new WinAppSandboxException("client_handle_isolation_failed");
                    }

                    if ((flags & 1) != 0)
                    {
                        if (!SetHandleInformation(handle, 1, 0))
                        {
                            throw new WinAppSandboxException("client_handle_isolation_failed");
                        }

                        restore.Add(handle);
                    }
                }

                return (launch ?? Process.Start)(start) ?? throw new WinAppSandboxException("client_start_failed");
            }
            finally
            {
                foreach (var handle in restore)
                {
                    _ = SetHandleInformation(handle, 1, 1);
                }
            }
        }
    }

    public string StandardError
    {
        get
        {
            lock (stderr)
            {
                return stderr.ToString();
            }
        }
    }

    public bool WaitForExit(TimeSpan timeout) => process.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue));

    public Result Complete(TimeSpan timeout)
    {
        if (!WaitForExit(timeout))
        {
            throw new WinAppSandboxException("command_timeout");
        }

        // A provider child must not keep its parent's inherited pipe handles open
        // forever. Bound the drain independently from the process-exit deadline.
        try
        {
            Task.WhenAll(stdoutReader, stderrReader).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is TimeoutException or IOException)
        {
            throw new WinAppSandboxException("command_stream_incomplete");
        }

        lock (stderr)
        {
            return new Result(process.ExitCode, StandardOutput, stderr.ToString());
        }
    }

    public Result CompleteAndDispose(TimeSpan timeout)
    {
        Result result;
        try
        {
            // One-shot commands have no input producer. Forwarding an open,
            // unwritten pipe into the provider must not delay their completion.
            process.StandardInput.Close();
            result = Complete(timeout);
        }
        catch (WinAppSandboxException operationError)
        {
            try
            {
                Dispose();
            }
            catch (WinAppSandboxException cleanupError)
            {
                throw new AggregateException("Modern Sandbox command failed and its streams did not close cleanly.", operationError, cleanupError);
            }

            throw;
        }

        Dispose();
        return result;
    }

    public WinAppSandboxException Failure(string defaultCode)
    {
        var result = Complete(TimeSpan.FromSeconds(1));
        return new WinAppSandboxException(result.ErrorCode == "command_failed" ? defaultCode : result.ErrorCode, result.ExitCode);
    }

    public void WaitForRecordingStart(TimeSpan timeout, Action requireLiveWorker)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            requireLiveWorker();
            if (HasExited)
            {
                throw Failure("recording_exited_early");
            }

            if (WinAppSandboxProtocol.RecordingStarted(StandardError))
            {
                return;
            }

            if (timer.Elapsed >= timeout)
            {
                throw new WinAppSandboxException("recording_start_timeout");
            }

            Thread.Sleep(100);
        }
    }

    public void RequestRecordingStop()
    {
        if (HasExited)
        {
            return;
        }

        try
        {
            process.StandardInput.WriteLine();
            process.StandardInput.Flush();
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            throw new WinAppSandboxException("recording_stop_pipe_failed");
        }
    }

    public void Terminate()
    {
        if (HasExited)
        {
            return;
        }

        try
        {
            // This Process retains the handle returned by our own Process.Start.
            // Do not reopen a PID, kill a process name, or walk an unowned tree.
            process.Kill();
            if (!WaitForExit(TimeSpan.FromSeconds(15)))
            {
                throw new WinAppSandboxException("owned_command_stop_timeout");
            }
        }
        catch (Win32Exception)
        {
            if (!HasExited)
            {
                throw new WinAppSandboxException("owned_command_stop_failed");
            }
        }
        catch (InvalidOperationException)
        {
            if (!HasExited)
            {
                throw new WinAppSandboxException("owned_command_stop_failed");
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Terminate();
        cancellation.Cancel();
        try
        {
            Task.WhenAll(stdoutReader, stderrReader).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is TimeoutException or IOException)
        {
            throw new WinAppSandboxException("command_stream_incomplete");
        }
        finally
        {
            disposed = true;
            process.Dispose();
            cancellation.Dispose();
        }
    }

    private async Task DrainAsync(StreamReader reader, StringBuilder output)
    {
        var buffer = new char[4096];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) != 0)
            {
                lock (output)
                {
                    var retained = Math.Min(count, OutputLimit - output.Length);
                    output.Append(buffer, 0, retained);
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancellation closes only our bounded, private in-memory diagnostics.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int kind);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(nint handle, out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

    internal sealed record Result(int ExitCode, string StandardOutput, string StandardError)
    {
        public string ErrorCode
        {
            get
            {
                var code = WinAppSandboxProtocol.SafeErrorCode(StandardError);
                return code == "command_failed" ? WinAppSandboxProtocol.SafeErrorCode(StandardOutput) : code;
            }
        }

        public string RequireSuccess()
        {
            if (ExitCode != 0)
            {
                throw new WinAppSandboxException(ErrorCode, ExitCode);
            }

            return StandardOutput;
        }
    }
}
