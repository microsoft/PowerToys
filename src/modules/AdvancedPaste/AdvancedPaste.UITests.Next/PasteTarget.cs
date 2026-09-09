// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

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
        var foreground = Invoke(WindowControl.GetForegroundWindowInfo);
        if (foreground.ProcessName is "SearchApp" or "SearchHost" or "StartMenuExperienceHost")
        {
            KeyboardHelper.SendKey(Key.Esc);
        }

        var handle = Handle;
        var originalTopMost = Invoke(() => form!.TopMost);
        void Activate() => Invoke(() =>
        {
            form!.Show();
            form.Activate();
            WindowControl.TryBringToForeground(handle);
            editor!.Focus();
            if (WindowControl.GetForegroundWindowHandle() != handle)
            {
                // Foreground lock can leave the new fixture behind maximized Settings.
                // Expose only our own caption for a real click, then restore its topmost state.
                form.TopMost = true;
                var bounds = WindowHelper.GetWindowBounds(handle);
                foreach (var x in new[] { bounds.Left + 100, bounds.Right - 160 })
                {
                    if (WindowControl.IsPointOwnedByWindow(handle, x, bounds.Top + 16))
                    {
                        MouseHelper.LeftClickAt(x, bounds.Top + 16);
                        break;
                    }
                }
            }
        });

        try
        {
            Activate();
            var ready = WaitHelper.WaitForStable(
                () => Invoke(() => (Foreground: WindowControl.GetForegroundWindowHandle(), EditorFocused: editor!.Focused, TopMost: form!.TopMost)),
                state => state.Foreground == handle && state.EditorFocused && state.TopMost == originalTopMost,
                timeoutMS: 10_000,
                requiredConsecutiveMatches: 2,
                recover: state =>
                {
                    if (state.Foreground == handle && state.EditorFocused)
                    {
                        Invoke(() => form!.TopMost = originalTopMost);
                    }
                    else
                    {
                        Activate();
                    }
                });
            Assert.IsTrue(
                ready.Succeeded,
                $"Paste destination did not acquire foreground and editor focus. Last state: {ready.LastObservation}; actual: {Invoke(WindowControl.GetForegroundWindowInfo)}.");
        }
        finally
        {
            Invoke(() => form!.TopMost = originalTopMost);
        }
    }

    internal void Paste()
    {
        Focus();
        Invoke(() => TestKeyboard.SendChord(Key.Ctrl, Key.V));
    }

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
