// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.GrabAndMove.UITests;

internal sealed class TestWindow : IDisposable
{
    private readonly TaskCompletionSource<IntPtr> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread windowThread;
    private System.Windows.Forms.Form? fixtureForm;
    private int stopRequested;
    private bool disposed;

    internal TestWindow()
    {
        windowThread = new Thread(RunWindow)
        {
            IsBackground = true,
            Name = "Grab And Move resizable fixture",
        };
        windowThread.SetApartmentState(ApartmentState.STA);
        windowThread.Start();

        try
        {
            Handle = ready.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IntPtr Handle { get; }

    internal Rectangle Bounds
    {
        get
        {
            var (left, top, right, bottom) = WindowHelper.GetWindowBounds(Handle);
            if (right <= left || bottom <= top)
            {
                throw new InvalidOperationException($"The fixture HWND {Handle} has no valid bounds.");
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }
    }

    internal void Focus()
    {
        KeyboardHelper.SendKey(Key.Esc);
        RaiseFixture();
        var bounds = Bounds;
        var x = bounds.Left + (bounds.Width / 2);
        var y = bounds.Top + (bounds.Height / 2);
        var point = WaitHelper.WaitForStable(
            () => WindowControl.IsPointOwnedByWindow(Handle, x, y),
            owned => owned,
            5_000,
            requiredConsecutiveMatches: 3,
            recover: _ => RaiseFixture());
        if (!point.Succeeded)
        {
            throw new InvalidOperationException(
                $"The fixture HWND {Handle} did not own ({x}, {y}); bounds={Bounds}, DPI={fixtureForm!.DeviceDpi}. " +
                $"Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        }

        MouseHelper.MoveTo(x, y);
        MouseHelper.LeftDown();
        MouseHelper.LeftUp();
        if (!WindowControl.WaitForForeground(Handle, 5_000, requiredConsecutiveMatches: 3))
        {
            throw new InvalidOperationException($"The fixture could not own foreground after a real client-area click: {WindowControl.GetForegroundWindowInfo()}.");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Exchange(ref stopRequested, 1);
        if (Handle != IntPtr.Zero && !WindowControl.TryCloseWindow(Handle.ToInt64()))
        {
            throw new InvalidOperationException($"Could not close the fixture HWND {Handle}.");
        }

        if (!windowThread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The fixture UI thread did not exit.");
        }
    }

    private void RaiseFixture()
    {
        var form = fixtureForm ?? throw new InvalidOperationException("The fixture form is not initialized.");
        form.InvokeAsync(new Action(() => WindowControl.TryBringToForeground(Handle)))
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }

    private void RunWindow()
    {
        try
        {
            var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            using var form = new System.Windows.Forms.Form
            {
                Text = "Grab And Move UI test fixture",
                BackColor = Color.White,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new Rectangle(
                    workArea.Left + (workArea.Width / 8),
                    workArea.Top + (workArea.Height / 8),
                    Math.Min(640, workArea.Width / 3),
                    Math.Min(400, workArea.Height / 3)),
            };
            fixtureForm = form;
            form.Shown += (_, _) =>
            {
                ready.TrySetResult(form.Handle);
                if (Volatile.Read(ref stopRequested) != 0)
                {
                    form.BeginInvoke(new Action(form.Close));
                }
            };
            System.Windows.Forms.Application.Run(form);
        }
        catch (Exception error)
        {
            ready.TrySetException(error);
        }
    }
}
