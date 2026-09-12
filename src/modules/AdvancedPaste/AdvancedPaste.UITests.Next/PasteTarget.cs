// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Forms = System.Windows.Forms;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

internal sealed class PasteTarget : IDisposable
{
    private readonly TaskCompletionSource<Forms.Form> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private Forms.RichTextBox? editor;
    private Forms.Form? form;
    private Exception? threadFailure;
    private int stopRequested;
    private bool disposed;

    internal PasteTarget()
    {
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Advanced Paste test destination",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            form = ready.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IntPtr Handle => Invoke(() => form!.Handle);

    internal string Text => Invoke(() => editor!.Text.ReplaceLineEndings("\n"));

    internal (IntPtr Handle, bool Visible, Forms.FormWindowState WindowState, IntPtr Foreground, bool EditorFocused) State =>
        Invoke(() => (form!.Handle, form.Visible, form.WindowState, WindowControl.GetForegroundWindowHandle(), editor!.Focused));

    internal void Clear() => Invoke(() =>
    {
        editor!.Clear();
        editor.SelectionFont = editor.Font;
        editor.SelectionColor = editor.ForeColor;
    });

    internal bool IsBold(int start, int length) => Invoke(() =>
    {
        editor!.Select(start, length);
        return editor.SelectionFont?.Bold == true;
    });

    internal void Focus()
    {
        var handle = Handle;
        TestWindow.SelectFromTaskbar(handle, "AdvancedPaste.UITests.Next");

        Invoke(() =>
        {
            editor!.Focus();
        });

        var ready = WaitHelper.WaitForStable(
            () => State,
            state => state.Visible && state.WindowState != Forms.FormWindowState.Minimized &&
                state.Foreground == handle && state.EditorFocused,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(
            ready.Succeeded,
            $"Paste destination did not acquire foreground and editor focus. Last state: {ready.LastObservation}; actual: {Invoke(WindowControl.GetForegroundWindowInfo)}.");
    }

    internal void Paste()
    {
        Focus();
        Invoke(() => KeyboardHelper.SendChord(Key.Ctrl, Key.V));
    }

    internal void CopyRichText(string rtf)
    {
        Focus();
        Invoke(() =>
        {
            editor!.Rtf = rtf;
            editor.SelectAll();
            KeyboardHelper.SendChord(Key.Ctrl, Key.C);
        });
    }

    internal Task<DataPackage> CaptureClipboardAsync() =>
        CaptureClipboardAsync((content, format) => content.GetDataAsync(format).AsTask());

    internal Task<DataPackage> CaptureClipboardAsync(Func<DataPackageView, string, Task<object>> readData) => Invoke(async () =>
    {
        ArgumentNullException.ThrowIfNull(readData);

        // Clipboard-backed format requests require the pumping STA, including continuations after await.
        var timeout = TimeSpan.FromSeconds(10);
        var timer = Stopwatch.StartNew();
        var format = "clipboard format enumeration";
        while (true)
        {
            try
            {
                if (timer.Elapsed >= timeout)
                {
                    throw new TimeoutException("The clipboard snapshot deadline expired.");
                }

                format = "clipboard format enumeration";
                var original = WinClipboard.GetContent();
                var snapshot = new DataPackage();
                foreach (var availableFormat in original.AvailableFormats)
                {
                    format = availableFormat;
                    var remaining = timeout - timer.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new TimeoutException("The clipboard snapshot deadline expired.");
                    }

                    var value = await readData(original, format).WaitAsync(remaining);
                    snapshot.SetData(format, value);
                }

                return snapshot;
            }
            catch (COMException exception) when (exception.HResult == unchecked((int)0x800401D0) && timer.Elapsed < timeout)
            {
                // Retry a fresh snapshot only for clipboard contention, without blocking the STA pump.
                await Task.Delay(100);
            }
            catch (Exception exception) when (exception is COMException or TimeoutException)
            {
                throw new InvalidOperationException($"Could not preserve clipboard format '{format}' (HRESULT 0x{exception.HResult:X8}).", exception);
            }
        }
    });

    internal void AssertText(string expected)
    {
        var result = WaitHelper.WaitForStable(
            () => Text,
            text => string.Equals(text, expected.ReplaceLineEndings("\n"), StringComparison.Ordinal),
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(
            result.Succeeded,
            $"Advanced Paste did not paste the expected text into the destination. Expected: '{expected}'; actual: '{result.LastObservation}'.");
    }

    internal void Invoke(Action action) => Invoke(() =>
    {
        action();
        return true;
    });

    internal T Invoke<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var window = form ?? throw new InvalidOperationException("The paste destination is not ready.");
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.BeginInvoke(new Action(() =>
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
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Interlocked.Exchange(ref stopRequested, 1);
        if (form is { IsDisposed: false })
        {
            Invoke(() => form.Close());
        }

        disposed = true;
        if (!thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The paste destination UI thread did not stop.");
        }

        if (threadFailure is not null)
        {
            throw new InvalidOperationException("The paste destination UI thread failed.", threadFailure);
        }
    }

    private void Run()
    {
        try
        {
            using var window = new Forms.Form
            {
                Text = "Advanced Paste test destination",
                StartPosition = Forms.FormStartPosition.CenterScreen,
                Size = new System.Drawing.Size(900, 360),
            };
            using var textBox = new Forms.RichTextBox
            {
                AccessibleName = "Pasted text",
                Dock = Forms.DockStyle.Fill,
                Multiline = true,
                AcceptsTab = true,
                Font = new System.Drawing.Font("Segoe UI", 14),
            };
            editor = textBox;
            window.Controls.Add(textBox);
            window.Shown += (_, _) =>
            {
                ready.TrySetResult(window);
                if (Volatile.Read(ref stopRequested) != 0)
                {
                    window.Close();
                }
            };

            if (Volatile.Read(ref stopRequested) == 0)
            {
                Forms.Application.Run(window);
            }
        }
        catch (Exception ex)
        {
            threadFailure = ex;
            ready.TrySetException(ex);
        }
    }
}
