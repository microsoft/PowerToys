// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class KeyboardInputWindow : IDisposable
{
    private readonly ManualResetEventSlim ready = new(false);
    private readonly ManualResetEventSlim closed = new(false);
    private readonly AutoResetEvent textChanged = new(false);
    private readonly Thread uiThread;
    private System.Windows.Forms.Form? form;
    private System.Windows.Forms.TextBox? input;
    private bool disposed;

    public KeyboardInputWindow()
    {
        uiThread = new Thread(RunWindow)
        {
            IsBackground = true,
            Name = "Keyboard Manager UI test input window",
        };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The keyboard input window did not open within 10 seconds.");
        }
    }

    public IntPtr Handle => Invoke(() => form!.Handle);

    public string Text => Invoke(() => input!.Text);

    public bool IsOpen => !closed.IsSet;

    public void SetText(string value) => Invoke(
        () =>
        {
            input!.Text = value;
            input.SelectionStart = input.TextLength;
            input.SelectionLength = 0;
        });

    public bool FocusInput(int timeoutMS = 10_000)
    {
        Invoke(
            () =>
            {
                form!.WindowState = System.Windows.Forms.FormWindowState.Normal;
                form.Show();
                form.Activate();
                form.BringToFront();
                input!.Focus();
            });

        return WindowControl.WaitForForeground(Handle, timeoutMS, requiredConsecutiveMatches: 2);
    }

    public bool WaitForText(string expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            if (Text.Equals(expected, StringComparison.Ordinal))
            {
                return true;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            textChanged.WaitOne(remaining > TimeSpan.FromMilliseconds(250) ? 250 : (int)remaining.TotalMilliseconds);
        }
        while (DateTime.UtcNow < deadline);

        return Text.Equals(expected, StringComparison.Ordinal);
    }

    public bool WaitForClosed(int timeoutMS) => closed.Wait(timeoutMS);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!closed.IsSet && form is not null)
        {
            try
            {
                form.BeginInvoke(form.Close);
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (uiThread.Join(TimeSpan.FromSeconds(5)))
        {
            textChanged.Dispose();
            closed.Dispose();
            ready.Dispose();
        }
    }

    private void RunWindow()
    {
        using var inputForm = new System.Windows.Forms.Form
        {
            Text = "Keyboard Manager UI test input",
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            Size = new System.Drawing.Size(640, 320),
        };
        using var inputBox = new System.Windows.Forms.TextBox
        {
            AccessibleName = "Keyboard input",
            Dock = System.Windows.Forms.DockStyle.Fill,
            Multiline = true,
        };

        form = inputForm;
        input = inputBox;
        inputBox.TextChanged += (_, _) => textChanged.Set();
        inputForm.FormClosed += (_, _) => closed.Set();
        inputForm.Shown += (_, _) =>
        {
            inputBox.Focus();
            ready.Set();
        };
        inputForm.Controls.Add(inputBox);
        System.Windows.Forms.Application.Run(inputForm);
    }

    private void Invoke(Action action)
    {
        if (form is null || form.IsDisposed)
        {
            throw new InvalidOperationException("The keyboard input window is closed.");
        }

        form.Invoke(action);
    }

    private T Invoke<T>(Func<T> action)
    {
        if (form is null || form.IsDisposed)
        {
            throw new InvalidOperationException("The keyboard input window is closed.");
        }

        return (T)form.Invoke(action);
    }
}
