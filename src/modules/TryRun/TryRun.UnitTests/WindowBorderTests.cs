// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class WindowBorderTests
{
    [TestMethod]
    public void RejectsAReusedPidAndInvalidProcessIdentity()
    {
        var identity = WindowsProcessIdentity.Capture((uint)Environment.ProcessId)!;
        Assert.IsNotNull(identity);
        using var valid = new WindowsProcessTree(identity);
        Assert.IsTrue(valid.IsRunning);
        using var stale = new WindowsProcessTree(identity with { CreationTime = identity.CreationTime + 1 });
        Assert.IsFalse(stale.IsRunning);
        Assert.IsFalse(stale.Contains(identity.ProcessId));
        Assert.IsNull(WindowsProcessIdentity.Capture(0));
        valid.Dispose();
        Assert.IsFalse(valid.IsRunning);
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WorkerAnnouncesTheActualWindowsProcessAfterSpawn()
    {
        using var session = new RunSession();
        WindowsProcessIdentity? announced = null;
        var wasAlive = false;
        var output = new ConcurrentQueue<string>();
        var result = await new WorkerClient(MultiBackendTests.Worker()).RunAsync(
            new ExecutionRequest("Write-Output $PID; Start-Sleep -Seconds 1", session.WorkingDirectory, session.TemporaryDirectory, 15),
            new MultiBackendTests.ImmediateProgress(message =>
            {
                if (message.Kind == WorkerMessage.ProcessStarted)
                {
                    announced = message.Process;
                    wasAlive = announced == WindowsProcessIdentity.Capture(announced!.ProcessId);
                }

                if (message.Kind == WorkerMessage.Output)
                {
                    output.Enqueue(message.Text);
                }
            }),
            CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode, string.Join(string.Empty, output));
        Assert.IsNotNull(announced);
        Assert.IsTrue(wasAlive);
        StringAssert.Contains(string.Join(string.Empty, output), announced.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var exited = new WindowsProcessTree(announced);
        Assert.IsFalse(exited.IsRunning);
    }

    [TestMethod]
    [DataRow(false, "stop")]
    [DataRow(true, "close")]
    [DataRow(false, "timeout")]
    [TestCategory("MXCIntegration")]
    public async Task MarksOnlyRunWindowsAndFollowsTheirLifetime(bool launchFromScript, string finish)
    {
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_GUI_TESTS") != "1")
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_GUI_TESTS=1 to validate borders against real Windows version dialogs.");
        }

        await OnDispatcherAsync(async () =>
        {
            using var ordinary = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "winver.exe")) { UseShellExecute = false })!;
            using var session = new RunSession();
            using var cancellation = new CancellationTokenSource();
            var failures = new List<string>();
            using var borders = new RunWindowBorders(failures.Add);
            var output = new List<string>();
            WindowsProcessIdentity? root = null;
            var request = launchFromScript
                ? new ExecutionRequest("$s = New-Object System.Diagnostics.ProcessStartInfo; $s.FileName = Join-Path $env:SystemRoot 'System32\\winver.exe'; $s.UseShellExecute = $false; $p = [System.Diagnostics.Process]::Start($s); $p.WaitForExit()", session.WorkingDirectory, session.TemporaryDirectory, 45)
                : new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, finish == "timeout" ? 8 : 45) { Kind = WorkloadKind.WindowsApplication, ApplicationPath = Path.Combine(Environment.SystemDirectory, "winver.exe") };
            var progress = new Progress<WorkerMessage>(message =>
            {
                output.Add(message.Text);
                if (message.Process is { } identity)
                {
                    root = identity;
                    borders.Start(identity);
                }

                if (message.Kind == WorkerMessage.Completed)
                {
                    borders.Dispose();
                }
            });
            var run = new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, progress, cancellation.Token);
            try
            {
                await UntilAsync(() => borders.Borders.Count != 0 || run.IsCompleted);
                Assert.IsFalse(run.IsCompleted, string.Join("\n", output));
                Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
                Assert.IsNotNull(root);
                await UntilAsync(() =>
                {
                    ordinary.Refresh();
                    return ordinary.MainWindowHandle != 0;
                });
                var target = borders.Borders.Keys.First();
                WindowBorderNative.GetWindowThreadProcessId(target, out var targetPid);
                Assert.AreNotEqual((uint)ordinary.Id, targetPid);
                Assert.AreEqual(!launchFromScript, targetPid == root.ProcessId, "The script's GUI child must also be tracked.");
                Assert.IsFalse(borders.Borders.ContainsKey(ordinary.MainWindowHandle));

                var foreground = GetForegroundWindow();
                var overlay = borders.Borders[target].Handle;
                var style = WindowBorderNative.GetWindowLongPtrW(overlay, WindowBorderNative.ExtendedStyle).ToInt64();
                Assert.AreEqual(WindowBorderNative.Transparent | WindowBorderNative.NoActivate | WindowBorderNative.ToolWindow, style & (WindowBorderNative.Transparent | WindowBorderNative.NoActivate | WindowBorderNative.ToolWindow));
                Assert.AreEqual((nint)(-1), SendMessageW(overlay, 0x0084, 0, 0));
                Assert.AreEqual(0L, style & 8, "An ordinary window's border must not be globally topmost.");

                WindowBorderNative.GetWindowRect(target, out var original);
                Assert.IsTrue(WindowBorderNative.SetWindowPos(target, 0, original.Left + 35, original.Top + 20, original.Right - original.Left + 40, original.Bottom - original.Top + 25, 0x0014));
                await Task.Delay(200);
                borders.Refresh();
                WindowBorderNative.GetFrameBounds(target, 9, out var expected, 16);
                WindowBorderNative.GetWindowRect(overlay, out var actual);
                Assert.AreEqual(expected, actual, "The border must follow the visible frame in physical pixels.");
                Assert.AreEqual(foreground, GetForegroundWindow(), "Moving a border must not steal focus.");
                Assert.AreEqual(overlay, WindowBorderNative.GetWindow(target, 3));

                // Cover the run with the ordinary instance; the border stays below
                // the ordinary window rather than drawing over its contents.
                Assert.IsTrue(WindowBorderNative.SetWindowPos(ordinary.MainWindowHandle, 0, expected.Left, expected.Top, 0, 0, 0x0011));
                borders.Refresh();
                Assert.AreEqual(overlay, WindowBorderNative.GetWindow(target, 3));
                Assert.IsFalse(borders.Borders.ContainsKey(ordinary.MainWindowHandle));
                Assert.AreNotEqual(overlay, GetForegroundWindow());

                // The first ordinary window can sit immediately below a topmost
                // one. Inserting its overlay after that HWND must not promote it.
                Assert.IsTrue(WindowBorderNative.SetWindowPos(ordinary.MainWindowHandle, -1, 0, 0, 0, 0, 0x0013));
                Assert.IsTrue(WindowBorderNative.SetWindowPos(target, 0, 0, 0, 0, 0, 0x0013));
                await Task.Delay(200);
                borders.Refresh();
                Assert.AreEqual(0L, WindowBorderNative.GetWindowLongPtrW(overlay, WindowBorderNative.ExtendedStyle).ToInt64() & 8);
                Assert.AreEqual(overlay, WindowBorderNative.GetWindow(target, 3), $"Target {target}, ordinary {ordinary.MainWindowHandle}, current border {borders.Borders[target].Handle}, above border {WindowBorderNative.GetWindow(overlay, 3)}, below border {WindowBorderNative.GetWindow(overlay, 2)}");

                Assert.IsTrue(WindowBorderNative.SetWindowPos(target, -1, 0, 0, 0, 0, 0x0013));
                await Task.Delay(200);
                borders.Refresh();
                Assert.AreEqual(8L, WindowBorderNative.GetWindowLongPtrW(overlay, WindowBorderNative.ExtendedStyle).ToInt64() & 8);
                Assert.AreEqual(overlay, WindowBorderNative.GetWindow(target, 3));
                Assert.IsTrue(WindowBorderNative.SetWindowPos(target, -2, 0, 0, 0, 0, 0x0013));
                Assert.IsTrue(WindowBorderNative.SetWindowPos(ordinary.MainWindowHandle, -2, 0, 0, 0, 0, 0x0013));
                await Task.Delay(200);
                borders.Refresh();
                Assert.AreEqual(0L, WindowBorderNative.GetWindowLongPtrW(overlay, WindowBorderNative.ExtendedStyle).ToInt64() & 8);

                ShowWindow(target, 6); // Minimize
                await Task.Delay(200);
                borders.Refresh();
                Assert.IsFalse(borders.Borders.ContainsKey(target));
                Assert.IsFalse(IsWindow(overlay));
                ShowWindow(target, 9); // Restore
                await UntilAsync(() => borders.Borders.ContainsKey(target));
                overlay = borders.Borders[target].Handle;

                ShowWindow(target, 3); // Maximize
                await Task.Delay(200);
                borders.Refresh();
                WindowBorderNative.GetFrameBounds(target, 9, out expected, 16);
                WindowBorderNative.GetWindowRect(overlay, out actual);
                Assert.AreEqual(expected, actual, "A maximized window's border must remain inside the visible frame.");

                if (finish == "stop")
                {
                    await cancellation.CancelAsync();
                    await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(15)));
                }
                else
                {
                    if (finish == "close")
                    {
                        PostMessageW(target, 0x0010, 0, 0); // WM_CLOSE
                    }

                    var result = await run.WaitAsync(TimeSpan.FromSeconds(15));
                    Assert.AreEqual(finish == "timeout", result.TimedOut);
                    if (finish == "close")
                    {
                        Assert.AreEqual(0, result.ExitCode, string.Join("\n", output));
                    }
                }

                await UntilAsync(() => borders.Borders.Count == 0);
                Assert.IsFalse(IsWindow(overlay));
                Assert.IsFalse(ordinary.HasExited, "Stopping the run must not close an unrelated ordinary instance.");
            }
            finally
            {
                await cancellation.CancelAsync();
                try
                {
                    await run.WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (OperationCanceledException)
                {
                }

                borders.Dispose();
                if (!ordinary.HasExited)
                {
                    ordinary.Kill();
                    await ordinary.WaitForExitAsync();
                }
            }
        });
    }

    [TestMethod]
    [TestCategory("GUIRendering")]
    public async Task BorderPaintsCyanEdgesAndLeavesTheApplicationVisible()
    {
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_GUI_TESTS") != "1")
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_GUI_TESTS=1 in an interactive desktop session to validate the rendered border.");
        }

        await OnDispatcherAsync(async () =>
        {
            // A disconnected/locked desktop can return an entirely empty bitmap
            // even for a solid drawing. Keep that environmental limit separate
            // from the native ownership, geometry and lifecycle integration tests.
            var control = new DrawingVisual();
            using (var drawing = control.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.Red, null, new System.Windows.Rect(0, 0, 10, 10));
            }

            var controlBitmap = new RenderTargetBitmap(10, 10, 96, 96, PixelFormats.Pbgra32);
            controlBitmap.Render(control);
            var controlPixels = new byte[400];
            controlBitmap.CopyPixels(controlPixels, 40, 0);
            if (controlPixels[3] == 0)
            {
                Assert.Inconclusive("This desktop cannot rasterize even the solid-color control. Reconnect/unlock the desktop and rerun the rendering test; the border pixels have not been verified.");
            }

            var border = new RunWindowBorders.BorderWindow { Width = 300, Height = 200 };
            try
            {
                border.Show();
                await Task.Delay(200);
                border.UpdateLayout();
                var width = (int)Math.Ceiling(border.ActualWidth);
                var height = (int)Math.Ceiling(border.ActualHeight);
                Assert.IsTrue(width > 20 && height > 20);
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)border.Content);
                var pixels = new byte[width * height * 4];
                bitmap.CopyPixels(pixels, width * 4, 0);
                var edge = ((2 * width) + (width / 2)) * 4;
                Assert.AreEqual((byte)195, pixels[edge]);
                Assert.AreEqual((byte)183, pixels[edge + 1]);
                Assert.AreEqual((byte)0, pixels[edge + 2]);
                Assert.AreEqual((byte)255, pixels[edge + 3], "The cyan edge must actually render.");
                var center = (((height / 2) * width) + (width / 2)) * 4;
                Assert.AreEqual((byte)0, pixels[center + 3], "The center must stay transparent instead of covering the application.");
            }
            finally
            {
                border.Close();
            }
        });
    }

    private static async Task UntilAsync(Func<bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate() && timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(predicate(), "Timed out waiting for the expected window state.");
    }

    private static async Task OnDispatcherAsync(Func<Task> action)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await action();
                    completed.TrySetResult();
                }
                catch (Exception exception)
                {
                    completed.TrySetException(exception);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(90));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
}
