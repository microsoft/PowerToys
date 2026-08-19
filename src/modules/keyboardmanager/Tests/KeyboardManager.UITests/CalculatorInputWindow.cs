// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class CalculatorInputWindow : IKeyboardInputWindow, IDisposable
{
    private const string WindowTitle = "Calculator";

    private readonly Session window;
    private bool disposed;

    public CalculatorInputWindow()
    {
        var existingHandles = WindowControl.EnumerateAllWindows()
            .Select(candidate => candidate.Hwnd.ToInt64())
            .ToHashSet();
        Session? launchedWindow = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "calc.exe",
                UseShellExecute = true,
            });
            Assert.IsNotNull(process, "System Calculator could not be started.");

            launchedWindow = WindowsFinder.WaitForWindow(
                candidate =>
                    candidate.Title.Equals(WindowTitle, StringComparison.OrdinalIgnoreCase) &&
                    !existingHandles.Contains(candidate.Hwnd),
                timeoutMS: 30_000);
            window = launchedWindow ?? throw new AssertFailedException("A new Calculator window did not open.");
            ClassName = WindowsFinder.ListAll()
                .First(candidate => candidate.Hwnd == window.WindowHandle)
                .ClassName;

            Assert.IsTrue(FocusInput(), "Calculator did not own foreground after launch.");
        }
        catch
        {
            if (launchedWindow is not null)
            {
                WindowControl.TryCloseWindow(launchedWindow.WindowHandle, timeoutMS: 3_000);
            }

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
        WindowControl.TryCloseWindow(window.WindowHandle, timeoutMS: 3_000);
    }
}
