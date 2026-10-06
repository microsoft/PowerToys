// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.AlwaysOnTop.UITests;

internal sealed class TestWindow : IDisposable
{
    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008L;
    private const string PinnedProperty = "AlwaysOnTop_Pinned";

    private readonly ManualResetEventSlim ready = new();
    private readonly Thread windowThread;
    private System.Windows.Forms.Form? form;
    private Exception? startupFailure;
    private long windowHandle;
    private int stopRequested;
    private bool disposed;

    private TestWindow(string title)
    {
        windowThread = new Thread(() => RunWindow(title))
        {
            IsBackground = true,
            Name = $"AlwaysOnTop UI test fixture: {title}",
        };
        windowThread.SetApartmentState(ApartmentState.STA);
        windowThread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(10)))
        {
            Dispose();
            throw new TimeoutException($"The test window '{title}' did not become ready.");
        }

        if (startupFailure is not null)
        {
            Dispose();
            throw new InvalidOperationException($"The test window '{title}' failed to start.", startupFailure);
        }
    }

    internal IntPtr Handle => new(Interlocked.Read(ref windowHandle));

    internal string ProcessFileName => Path.GetFileName(Environment.ProcessPath)
        ?? throw new InvalidOperationException("The UI test process file name is unavailable.");

    internal static TestWindow Create(string title)
    {
        var window = new TestWindow(title);
        try
        {
            window.Focus();
            return window;
        }
        catch
        {
            window.Dispose();
            throw;
        }
    }

    internal bool IsPinned =>
        GetPropW(RequireLiveHandle(), PinnedProperty) != IntPtr.Zero;

    internal bool IsTopmost =>
        (GetWindowLongPtrW(RequireLiveHandle(), GwlExStyle).ToInt64() & WsExTopmost) != 0;

    internal bool IsMinimized => IsIconic(RequireLiveHandle());

    internal bool IsMaximized => IsZoomed(RequireLiveHandle());

    internal (int Left, int Top, int Right, int Bottom) VisibleFrameBounds
    {
        get
        {
            var handle = RequireLiveHandle();
            var result = DwmGetWindowAttribute(
                handle,
                DwmExtendedFrameBoundsAttribute,
                out var bounds,
                Marshal.SizeOf<Rect>());
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            return (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }
    }

    internal void Focus()
    {
        Invoke(
            () =>
            {
                form!.Show();
                form.Activate();
                form.Focus();
            });

        var handle = RequireLiveHandle();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var foreground = WindowControl.GetForegroundWindowInfo();
            DesktopHygiene.DismissForegroundShellSurface(foreground);

            WindowControl.TryBringToForeground(handle);
            var fixtureBounds = WindowHelper.GetWindowBounds(handle);
            if (fixtureBounds.Right > fixtureBounds.Left && fixtureBounds.Bottom > fixtureBounds.Top)
            {
                MouseHelper.LeftClickAt((fixtureBounds.Left + fixtureBounds.Right) / 2, fixtureBounds.Top + 16);
            }

            if (WindowControl.WaitForForeground(handle, timeoutMS: 2_000, requiredConsecutiveMatches: 2))
            {
                return;
            }
        }

        var currentForeground = WindowControl.GetForegroundWindowInfo();
        throw new InvalidOperationException(
            $"The fixture HWND {handle} could not own foreground. Current foreground: " +
            $"HWND={currentForeground.Hwnd}, process='{currentForeground.ProcessName}', title='{currentForeground.Title}'.");
    }

    internal void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var currentForm = form ?? throw new InvalidOperationException("The fixture window is not available.");
        InvokeWithTimeout(
            currentForm,
            () =>
            {
                action();
                return true;
            });
    }

    internal T Invoke<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var currentForm = form ?? throw new InvalidOperationException("The fixture window is not available.");
        return InvokeWithTimeout(currentForm, action);
    }

    internal System.Windows.Forms.Form GetForm()
    {
        return form ?? throw new InvalidOperationException("The fixture window is not available.");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Exchange(ref stopRequested, 1);
        try
        {
            form?.BeginInvoke(new Action(() => form.Close()));
        }
        catch (InvalidOperationException)
        {
        }

        if (windowThread.Join(TimeSpan.FromSeconds(5)))
        {
            ready.Dispose();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetPropW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    private const int DwmExtendedFrameBoundsAttribute = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    private IntPtr RequireLiveHandle()
    {
        var handle = Handle;
        if (handle == IntPtr.Zero || !IsWindow(handle))
        {
            throw new InvalidOperationException("The fixture window handle is no longer valid.");
        }

        return handle;
    }

    private static T InvokeWithTimeout<T>(System.Windows.Forms.Form target, Func<T> action)
    {
        if (!target.InvokeRequired)
        {
            return action();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        target.BeginInvoke(
            new Action(
                () =>
                {
                    try
                    {
                        completion.SetResult(action());
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                }));

        if (!completion.Task.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The fixture window UI thread did not respond within 10 seconds.");
        }

        return completion.Task.GetAwaiter().GetResult();
    }

    private void RunWindow(string title)
    {
        try
        {
            using var fixtureForm = new System.Windows.Forms.Form
            {
                BackColor = System.Drawing.Color.White,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable,
                Height = 420,
                Left = 440,
                MaximizeBox = true,
                MinimizeBox = true,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Text = title,
                Top = 260,
                TopMost = false,
                Width = 680,
            };

            fixtureForm.Controls.Add(
                new System.Windows.Forms.Label
                {
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 20),
                    Location = new System.Drawing.Point(40, 40),
                    Text = title,
                });

            fixtureForm.HandleCreated += (_, _) =>
            {
                Interlocked.Exchange(ref windowHandle, fixtureForm.Handle.ToInt64());
            };
            fixtureForm.HandleDestroyed += (_, _) =>
            {
                Interlocked.Exchange(ref windowHandle, 0);
            };
            fixtureForm.Shown += (_, _) =>
            {
                form = fixtureForm;
                Interlocked.Exchange(ref windowHandle, fixtureForm.Handle.ToInt64());
                if (Volatile.Read(ref stopRequested) != 0)
                {
                    fixtureForm.BeginInvoke(new Action(fixtureForm.Close));
                    return;
                }

                ready.Set();
            };

            if (Volatile.Read(ref stopRequested) != 0)
            {
                return;
            }

            System.Windows.Forms.Application.Run(fixtureForm);
        }
        catch (Exception ex)
        {
            startupFailure = ex;
            if (Volatile.Read(ref stopRequested) == 0)
            {
                ready.Set();
            }
        }
    }
}
