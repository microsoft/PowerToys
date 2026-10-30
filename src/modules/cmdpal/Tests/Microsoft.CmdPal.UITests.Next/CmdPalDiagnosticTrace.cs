// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

internal sealed class CmdPalDiagnosticTrace : IDisposable
{
    private sealed record ProcessState(int Id, string Name, double? CpuMilliseconds, long? WorkingSetBytes, int? ThreadCount, bool? Responding, string? Error);

    private readonly TestContext context;
    private readonly Session session;
    private readonly string path;
    private readonly StreamWriter writer;
    private readonly object gate = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly Task sampler;
    private string phase = "starting";
    private bool disposed;

    public CmdPalDiagnosticTrace(TestContext context, Session session)
    {
        this.context = context;
        this.session = session;

        // Keep the original outside MSTest's disposable deployment directory, even for passing tests.
        var deploymentDirectory = context.TestRunDirectory ?? throw new InvalidOperationException("The test run directory is unavailable.");
        var directory = Directory.GetParent(deploymentDirectory)?.FullName ?? throw new InvalidOperationException("The test results root is unavailable.");
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, $"cmdpal-state-{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            AutoFlush = true,
        };
        Record(
            "configuration",
            new
            {
                Test = context.TestName,
                context.ResultsDirectory,
                context.TestRunDirectory,
                context.TestResultsDirectory,
                context.TestRunResultsDirectory,
                OS = RuntimeInformation.OSDescription,
                Runtime = RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSArchitecture,
                RuntimeInformation.ProcessArchitecture,
                Environment.ProcessorCount,
                EnvironmentConfig.Platform,
                EnvironmentConfig.IsInPipeline,
                EnvironmentConfig.UseInstallerForTest,
                InitialWindow = session.WindowHandle,
                session.ProcessId,
                session.ProcessName,
                AppExecutable = SessionHelper.GetExecutablePath(PowerToysModule.CommandPalette),
                WinappExecutable = Environment.GetEnvironmentVariable("WINAPP_CLI_PATH"),
                WinappTimeout = Environment.GetEnvironmentVariable("WINAPP_CLI_INVOKE_TIMEOUT_SECONDS"),
            });
        context.WriteLine($"CmdPal navigation/native-health trace: {path}");

        // Never add a competing UIA client while the foreground test's CLI call may be hung.
        sampler = Task.Run(() =>
        {
            do
            {
                CaptureNativeState();
            }
            while (!stop.Token.WaitHandle.WaitOne(1_000));
        });
    }

    public void Record(string nextPhase, object? data = null)
    {
        Volatile.Write(ref phase, nextPhase);
        Write("phase", data);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        stop.Cancel();
        try
        {
            sampler.GetAwaiter().GetResult();
            CaptureNativeState();
            Record("trace-complete");
        }
        finally
        {
            lock (gate)
            {
                disposed = true;
                writer.Dispose();
            }

            stop.Dispose();
            context.AddResultFile(path);
        }
    }

    private void CaptureNativeState()
    {
        var observation = Stopwatch.StartNew();
        var processes = Process.GetProcessesByName("Microsoft.CmdPal.UI")
            .Concat(Process.GetProcessesByName("winapp"))
            .Select(ReadProcess)
            .ToArray();
        var ids = processes.Where(process => process.Name == "Microsoft.CmdPal.UI")
            .Select(process => process.Id).Append(session.ProcessId).Distinct().ToArray();
        var windows = WindowControl.EnumerateProcessWindows(ids)
            .Select(window =>
            {
                bool? isCloaked = null;
                string? error = null;
                try
                {
                    isCloaked = WindowHelper.IsWindowCloaked(window.Hwnd);
                }
                catch (InvalidOperationException exception)
                {
                    error = exception.Message;
                }

                return new
                {
                    Hwnd = window.Hwnd.ToInt64(),
                    window.ProcessId,
                    window.ClassName,
                    window.Title,
                    window.Width,
                    window.Height,
                    window.IsVisible,
                    IsCloaked = isCloaked,
                    Error = error,
                };
            }).ToArray();
        var foreground = WindowControl.GetForegroundWindowInfo();
        Write(
            "native-sample",
            new
            {
                Processes = processes,
                Windows = windows,
                Foreground = new
                {
                    Hwnd = foreground.Hwnd.ToInt64(),
                    foreground.ProcessId,
                    foreground.ProcessName,
                    foreground.Title,
                    foreground.ClassName,
                    foreground.IsElevated,
                },
                ObservationMilliseconds = observation.ElapsedMilliseconds,
            });
    }

    private static ProcessState ReadProcess(Process process)
    {
        using (process)
        {
            var id = process.Id;
            var name = string.Empty;
            try
            {
                name = process.ProcessName;
                return new ProcessState(
                    id,
                    name,
                    process.TotalProcessorTime.TotalMilliseconds,
                    process.WorkingSet64,
                    process.Threads.Count,
                    process.Responding,
                    null);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
                return new ProcessState(id, name, null, null, null, null, $"{exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private void Write(string kind, object? data)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            writer.WriteLine(JsonSerializer.Serialize(new
            {
                Utc = DateTimeOffset.UtcNow,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                Phase = Volatile.Read(ref phase),
                Kind = kind,
                Data = data,
            }));
        }
    }
}
