// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

internal static class MouseUtilsTestHelper
{
    private const int SpiGetClientAreaAnimation = 0x1042;
    private const int SpiSetClientAreaAnimation = 0x1043;

    internal const string InputOutputNavItemId = "InputOutputNavItem";
    internal const string MouseUtilitiesNavItemId = "MouseUtilitiesNavItem";

    internal static void NavigateToMouseUtilities(UITestBase testBase)
    {
        Step(testBase, "Navigating to Mouse Utilities settings");
        if (!testBase.Session.Has(By.AccessibilityId(MouseUtilitiesNavItemId), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(InputOutputNavItemId), 5_000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(MouseUtilitiesNavItemId), 5_000).Click(msPostAction: 800);
    }

    internal static ToggleSwitch SetModuleEnabled(UITestBase testBase, string toggleId, bool enabled)
    {
        Step(testBase, $"Setting {toggleId} to {(enabled ? "On" : "Off")}");
        var expectedState = enabled ? "On" : "Off";
        ToggleSwitch? toggle = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            toggle = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId(toggleId), 10_000);
            var actualState = toggle.GetProperty("ToggleState");
            if (actualState.Equals(expectedState, StringComparison.OrdinalIgnoreCase))
            {
                return toggle;
            }

            var oppositeState = enabled ? "Off" : "On";
            if (!actualState.Equals(oppositeState, StringComparison.OrdinalIgnoreCase))
            {
                Step(testBase, $"{toggleId} returned unreadable ToggleState '{actualState}' on attempt {attempt}/3; reacquiring it");
                if (toggle.WaitForProperty("ToggleState", expectedState, 3_000))
                {
                    return toggle;
                }

                continue;
            }

            toggle.Invoke(msPostAction: 500);
            if (toggle.WaitForProperty("ToggleState", expectedState, 15_000))
            {
                return toggle;
            }

            Step(testBase, $"{toggleId} did not reach {expectedState} on attempt {attempt}/3; reacquiring it");
        }

        Assert.Fail($"{toggleId} did not reach {expectedState} after three coordinate-free attempts.");
        return toggle!;
    }

    internal static WindowControl.ProcessWindow WaitForWindowClass(string className, int timeoutMs = 10_000)
    {
        var result = WaitHelper.WaitForStable(
            () => WindowControl.EnumerateAllWindows().FirstOrDefault(window =>
                window.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)),
            window => window.Hwnd != IntPtr.Zero,
            timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);

        Assert.IsTrue(result.Succeeded, $"Top-level window class '{className}' did not appear within {timeoutMs} ms.");
        return result.LastObservation;
    }

    internal static void ReplaceModuleSettings(string moduleName, string settingsJson)
    {
        SettingsConfigHelper.UpdateModuleSettings(
            moduleName,
            settingsJson,
            current =>
            {
                var desired = JsonNode.Parse(settingsJson)?.AsObject()
                    ?? throw new InvalidOperationException($"Settings seed for '{moduleName}' is not a JSON object.");
                current.Clear();
                foreach (var property in desired)
                {
                    current[property.Key] = property.Value?.DeepClone();
                }
            });
    }

    internal static void RunWithClientAreaAnimationsEnabled(Action action)
        => RunWithCleanup(action, PreserveClientAreaAnimationsEnabled());

    private static void RunWithCleanup(Action action, IDisposable cleanup)
    {
        Exception? actionFailure = null;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            actionFailure = ex;
        }

        Exception? restoreFailure = null;
        try
        {
            cleanup.Dispose();
        }
        catch (Exception ex)
        {
            restoreFailure = ex;
        }

        if (actionFailure is not null)
        {
            if (restoreFailure is not null)
            {
                throw new AggregateException(
                    "The test action failed and the Windows client-area animation setting could not be restored.",
                    actionFailure,
                    restoreFailure);
            }

            ExceptionDispatchInfo.Capture(actionFailure).Throw();
        }

        if (restoreFailure is not null)
        {
            ExceptionDispatchInfo.Capture(restoreFailure).Throw();
        }
    }

    internal static void DisposeAll(params IDisposable?[] snapshots)
    {
        var failures = new List<Exception>();
        foreach (var snapshot in snapshots)
        {
            try
            {
                snapshot?.Dispose();
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Multiple Mouse Utils test-state restorations failed.", failures);
        }
    }

    internal static Color Blend(Color foreground, Color background, int alpha)
    {
        var inverse = 255 - alpha;
        return Color.FromArgb(
            ((foreground.R * alpha) + (background.R * inverse) + 127) / 255,
            ((foreground.G * alpha) + (background.G * inverse) + 127) / 255,
            ((foreground.B * alpha) + (background.B * inverse) + 127) / 255);
    }

    internal static Color GetStablePixel(int x, int y)
    {
        Color? previous = null;
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(x, y),
            color =>
            {
                var matchesPrevious = previous.HasValue && color.ToArgb() == previous.Value.ToArgb();
                previous = color;
                return matchesPrevious;
            },
            2_000,
            requiredConsecutiveMatches: 4,
            pollIntervalMS: 100);
        return result.LastObservation;
    }

    internal static void AssertPixelNear(int x, int y, Color expected, int tolerance, string description)
    {
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(x, y),
            actual => IsNear(actual, expected, tolerance),
            5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(
            result.Succeeded,
            $"Unexpected {description} at ({x},{y}). Expected {expected}; observed {result.LastObservation}.");
    }

    internal static bool IsNear(Color actual, Color expected, int tolerance) =>
        Math.Abs(actual.R - expected.R) <= tolerance &&
        Math.Abs(actual.G - expected.G) <= tolerance &&
        Math.Abs(actual.B - expected.B) <= tolerance;

    internal static double Distance(int x1, int y1, int x2, int y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    internal sealed class NotepadFixture : IDisposable
    {
        private const string ProcessName = "notepad";
        private readonly string filePath;
        private readonly bool ownsProcess;
        private bool disposed;

        private NotepadFixture(string filePath, Session window, bool ownsProcess)
        {
            this.filePath = filePath;
            Window = window;
            this.ownsProcess = ownsProcess;
        }

        internal Session Window { get; }

        internal static NotepadFixture Start()
        {
            var existingProcessIds = GetProcessIds();
            var filePath = Path.Combine(Path.GetTempPath(), $"PowerToys-MouseUtils-{Guid.NewGuid():N}.txt");
            File.WriteAllText(filePath, string.Empty);
            var fileName = Path.GetFileName(filePath);
            var baseFileName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                Session? window = null;
                for (var attempt = 1; attempt <= 2 && window is null; attempt++)
                {
                    using var launcher = Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true,
                    });
                    Assert.IsNotNull(launcher, "Could not start the Notepad fixture.");
                    window = WindowsFinder.WaitForWindowByApp(
                        ProcessName,
                        candidate =>
                            candidate.Title.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                            candidate.Title.Contains($"{baseFileName} - Notepad", StringComparison.OrdinalIgnoreCase),
                        timeoutMS: 15_000);
                }

                Assert.IsNotNull(window, $"Notepad did not open the unique fixture document '{fileName}'.");
                return new NotepadFixture(filePath, window, !existingProcessIds.Contains(window.ProcessId));
            }
            catch
            {
                File.Delete(filePath);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            var closed = CloseDocumentTab();
            if (!closed && ownsProcess)
            {
                try
                {
                    using var process = Process.GetProcessById(Window.ProcessId);
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                    closed = true;
                }
                catch
                {
                }
            }

            if (closed)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private bool CloseDocumentTab()
        {
            var windowHandle = new IntPtr(Window.WindowHandle);
            if (!WindowControl.WaitForForeground(windowHandle, 3_000, requiredConsecutiveMatches: 2))
            {
                return false;
            }

            try
            {
                KeyboardHelper.PressKey(Key.Ctrl);
                Thread.Sleep(50);
                KeyboardHelper.SendKey(Key.W);
            }
            finally
            {
                KeyboardHelper.ReleaseKey(Key.Ctrl);
            }

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (!IsOpen())
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return !IsOpen();
        }

        private bool IsOpen() => WindowsFinder.ListByApp(ProcessName).Any(candidate =>
            candidate.Hwnd == Window.WindowHandle &&
            (candidate.Title.Contains(Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase) ||
             candidate.Title.Contains(Path.GetFileNameWithoutExtension(filePath), StringComparison.OrdinalIgnoreCase)));

        private static HashSet<int> GetProcessIds()
        {
            var processes = Process.GetProcessesByName(ProcessName);
            try
            {
                return processes.Select(process => process.Id).ToHashSet();
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }

    internal static IDisposable PreserveClientAreaAnimationsEnabled()
    {
        var originalValue = 0;
        Assert.IsTrue(
            SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref originalValue, 0),
            "Could not read the Windows client-area animation setting before module launch.");

        if (originalValue == 0)
        {
            var enabled = 1;
            Assert.IsTrue(
                SystemParametersInfo(SpiSetClientAreaAnimation, 0, ref enabled, 0),
                "Could not enable Windows client-area animations before module launch.");
        }

        return new ClientAreaAnimationsSnapshot(originalValue);
    }

    internal static void Step(UITestBase testBase, string message) =>
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref int pvParam, int fWinIni);

    private sealed class ClientAreaAnimationsSnapshot(int originalValue) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (originalValue == 0)
            {
                var disabled = 0;
                var restored = SystemParametersInfo(SpiSetClientAreaAnimation, 0, ref disabled, 0);
                if (!restored)
                {
                    throw new InvalidOperationException(
                        $"Could not restore the Windows client-area animation setting. Win32 error: {Marshal.GetLastWin32Error()}.");
                }
            }
        }
    }
}
