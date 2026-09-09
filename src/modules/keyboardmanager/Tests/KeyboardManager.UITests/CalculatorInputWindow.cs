// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class CalculatorInputWindow : IKeyboardInputWindow, IDisposable
{
    private const string ApplicationFrameWindowClass = "ApplicationFrameWindow";
    private const string WindowTitle = "Calculator";

    private readonly Session window;
    private bool disposed;

    public CalculatorInputWindow()
    {
        Assert.IsTrue(CloseCalculatorWindows(timeoutMS: 5_000), "A stale Calculator window could not be closed before launch.");
        Session? launchedWindow = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "calc.exe",
                UseShellExecute = true,
            });
            Assert.IsNotNull(process, "System Calculator could not be started.");

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                launchedWindow = WindowsFinder.WaitForWindow(
                    candidate => IsCalculatorWindow(candidate) &&
                        candidate.ClassName.Equals(ApplicationFrameWindowClass, StringComparison.OrdinalIgnoreCase),
                    timeoutMS: 15_000);
            }

            launchedWindow ??= WindowsFinder.WaitForWindow(
                IsCalculatorWindow,
                timeoutMS: 15_000);
            window = launchedWindow ?? throw new AssertFailedException("A new Calculator window did not open.");
            ClassName = WindowsFinder.ListAll()
                .First(candidate => candidate.Hwnd == window.WindowHandle)
                .ClassName;

            Assert.IsTrue(FocusInput(), "Calculator did not own foreground after launch.");
        }
        catch
        {
            CloseCalculatorWindows(timeoutMS: 3_000);
            throw;
        }
    }

    public IntPtr Handle => new(window.WindowHandle);

    public string ClassName { get; }

    public bool FocusInput(int timeoutMS = 10_000)
    {
        window.EnsureForeground();
        return WindowControl.WaitForForeground(Handle, timeoutMS, requiredConsecutiveMatches: 2);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CloseCalculatorWindows(timeoutMS: 3_000);
    }

    private static bool CloseCalculatorWindows(int timeoutMS)
    {
        foreach (var candidate in WindowsFinder.ListAll().Where(IsCalculatorWindow))
        {
            WindowControl.TryCloseWindow(candidate.Hwnd, timeoutMS);
        }

        return !WindowsFinder.ListAll().Any(IsCalculatorWindow);
    }

    private static bool IsCalculatorWindow(WindowsFinder.WindowInfo candidate) =>
        candidate.Title.Equals(WindowTitle, StringComparison.OrdinalIgnoreCase);
}
