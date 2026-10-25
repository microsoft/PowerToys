// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.AltWindowCycle.UITests;

internal sealed class WindowFixture : IDisposable
{
    internal static readonly Color[] Colors = [Color.FromArgb(180, 30, 70), Color.FromArgb(35, 170, 85), Color.FromArgb(35, 90, 190)];
    internal static readonly Color OtherColor = Color.FromArgb(220, 50, 200);

    private static readonly Lazy<string> OtherDirectory = new(CreateOtherApplicationCopy);
    private readonly List<Process> processes = [];
    private readonly TestContext context;

    internal WindowFixture(TestContext context)
    {
        this.context = context;
        try
        {
            var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixture");
            var executable = Path.Combine(fixtureDirectory, "AltWindowCycle.TestApp.exe");
            Assert.IsTrue(File.Exists(executable), $"Fixture executable was not staged: {executable}");
            var otherExecutable = Path.Combine(OtherDirectory.Value, Path.GetFileName(executable));

            Windows = Colors.Select((color, index) => Open(executable, $"Window Hopper source {index + 1}", color)).ToArray();
            OtherWindow = Open(otherExecutable, "Window Hopper different application", OtherColor);
            Assert.HasCount(4, processes.Select(process => process.Id).Distinct().ToArray(), "Every source must have its own live process.");
            Assert.IsTrue(processes.All(process => !process.HasExited), "A fixture process exited before the test began.");
            CollectionAssert.AreEqual(
                new[] { executable, executable, executable, otherExecutable },
                processes.Select(process => process.MainModule!.FileName).ToArray(),
                "The fixture must provide three same-image processes and one different-image process.");
            ArrangeForegroundOrder();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IntPtr[] Windows { get; } = [];

    internal IntPtr OtherWindow { get; }

    internal static void RemoveApplicationCopy()
    {
        if (OtherDirectory.IsValueCreated && Directory.Exists(OtherDirectory.Value))
        {
            Directory.Delete(OtherDirectory.Value, recursive: true);
        }
    }

    internal void ArrangeForegroundOrder()
    {
        // Interleave the foreign application in Z-order so an unfiltered cycle cannot accidentally pass.
        foreach (var window in new[] { Windows[2], OtherWindow, Windows[1], Windows[0] })
        {
            Focus(window);
        }
    }

    internal void Focus(IntPtr window)
    {
        context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Focusing fixture HWND 0x{window.ToInt64():X}.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(window, 10_000, requiredConsecutiveMatches: 3),
            $"Fixture did not acquire foreground. Actual: {WindowControl.GetForegroundWindowInfo()}.");
    }

    public void Dispose()
    {
        var errors = new List<Exception>();
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5_000))
                    {
                        process.Kill(entireProcessTree: true);
                        Assert.IsTrue(process.WaitForExit(10_000), $"Fixture PID {process.Id} did not exit.");
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
        }

        processes.Clear();
        if (errors.Count > 0)
        {
            throw new AggregateException("Could not stop the test-owned window fixtures.", errors);
        }
    }

    private static string CreateOtherApplicationCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"AltWindowCycleFixture-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixture")))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            return directory;
        }
        catch (Exception copyError)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Could not create or clean up the different-application fixture copy.", copyError, cleanupError);
            }

            throw;
        }
    }

    private IntPtr Open(string executable, string title, Color color)
    {
        context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting fixture '{title}' from '{executable}'.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(title);
        start.ArgumentList.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
        var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {title}.");
        processes.Add(process);
        Assert.IsFalse(ElevationHelper.IsProcessElevated(process.Id), "Fixture applications must run as a normal user.");
        var ready = WaitHelper.WaitForStable(
            () => WindowControl.EnumerateProcessWindows([process.Id]).Where(window => window.IsVisible && window.Title == title).ToArray(),
            windows => windows is { Length: 1 },
            20_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(ready.Succeeded, $"Fixture '{title}' did not expose exactly one visible window. Exited: {process.HasExited}.");
        return ready.LastObservation![0].Hwnd;
    }
}
