// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// An ordinary app to type triggers into: a rich text box in a window of the test process, on a UI thread of its own.
/// The tests read what Text Expander put there straight from the control, so no other app is involved.
/// </summary>
internal sealed class TypingTarget : IDisposable
{
    public const string Title = "Text Expander UI test target";

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    private readonly TaskCompletionSource<Forms.Form> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private readonly Action<string> log;
    private Forms.RichTextBox? editor;
    private Forms.Form? form;
    private Exception? threadFailure;
    private int stopRequested;
    private bool disposed;

    public TypingTarget(Action<string> log)
    {
        this.log = log;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = Title,
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

    public IntPtr Handle => Invoke(() => form!.Handle);

    /// <summary>Gets the text in the box, with <c>\n</c> line breaks.</summary>
    public string Text => Invoke(() => editor!.Text.ReplaceLineEndings("\n"));

    private (IntPtr Foreground, bool EditorFocused, bool Minimized) State =>
        Invoke(() => (WindowControl.GetForegroundWindowHandle(), editor!.Focused, form!.WindowState == Forms.FormWindowState.Minimized));

    /// <summary>Empties the box without typing, so Text Expander sees no keys.</summary>
    public void Clear() => Invoke(() => editor!.Clear());

    /// <summary>
    /// Clicks into the box, as a person does before typing, and puts the caret at the end. A click also clears what
    /// Text Expander remembers of earlier typing.
    /// </summary>
    /// <remarks>
    /// Text Expander checks each window that comes in front before it reads keys there. When the click activates the
    /// window, that check runs as it comes in front. Windows can refuse to raise a background process's window over
    /// the window in front, as it does here for the window's first activation; it's then brought forward with
    /// AttachThreadInput, as the engine itself hands focus back after a question. Such an activation reports its
    /// foreground change before the window is in front, so the engine checks the window at the first key it sees
    /// there, which it doesn't record. A Shift press is that key, so the trigger typed next is read whole.
    /// </remarks>
    public void Focus()
    {
        var handle = Handle;
        string failure = string.Empty;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Invoke(() =>
            {
                if (form!.WindowState == Forms.FormWindowState.Minimized)
                {
                    form.WindowState = Forms.FormWindowState.Normal;
                }
            });

            var (x, y) = Invoke(() =>
            {
                var bounds = editor!.RectangleToScreen(editor.ClientRectangle);
                return (bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
            });

            var activatedDirectly = false;
            if (WindowControl.GetForegroundWindowHandle() != handle)
            {
                // On top only while it's clicked, so it never covers Settings or the editor afterwards. Form.TopMost would
                // also try to activate the window; only the click may do that.
                SetTopmost(handle, topmost: true);
                try
                {
                    var reachable = WaitHelper.WaitForStable(() => WindowControl.IsPointOwnedByWindow(handle, x, y), owned => owned, timeoutMS: 1_000);
                    if (!reachable.Succeeded)
                    {
                        log($"The typing target is behind {DescribeWindowAt(handle, x, y)}; bringing it forward (focus attempt {attempt})");
                        WindowControl.TryBringToForeground(handle);
                        activatedDirectly = true;
                    }
                }
                finally
                {
                    SetTopmost(handle, topmost: false);
                }
            }

            if (WindowControl.IsPointOwnedByWindow(handle, x, y))
            {
                MouseHelper.LeftClickAt(x, y);
            }

            Invoke(() =>
            {
                editor!.Focus();
                editor.Select(editor.TextLength, 0);
            });

            var result = WaitHelper.WaitForStable(
                () => State,
                state => state.Foreground == handle && state.EditorFocused && !state.Minimized,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2);
            if (result.Succeeded)
            {
                if (activatedDirectly)
                {
                    TextInput.Press(0x10);
                }

                // Text Expander checks the window in front off its keyboard hook's thread.
                Thread.Sleep(400);
                return;
            }

            failure = $"last state {result.LastObservation}; foreground {WindowControl.GetForegroundWindowInfo()}";
            log($"The typing target did not get keyboard focus (focus attempt {attempt}): {failure}");
        }

        Assert.Fail($"The typing target did not get keyboard focus: {failure}.");
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
            try
            {
                Invoke(() => form.Close());
            }
            catch (TimeoutException)
            {
            }
        }

        disposed = true;
        if (!thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The typing target's UI thread did not stop.");
        }

        if (threadFailure is not null)
        {
            throw new InvalidOperationException("The typing target's UI thread failed.", threadFailure);
        }
    }

    private T Invoke<T>(Func<T> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var window = form ?? throw new InvalidOperationException("The typing target is not ready.");
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

    private void Invoke(Action action) => Invoke(() =>
    {
        action();
        return true;
    });

    /// <summary>Puts the window in or out of the topmost band without activating it, and checks it got there.</summary>
    private void SetTopmost(IntPtr window, bool topmost)
    {
        // A window just shown can still lose a z-order change to its own first layout pass, so it's checked and repeated.
        WaitHelper.WaitForStable(
            () =>
            {
                Invoke(() => SetWindowPos(window, topmost ? HwndTopmost : HwndNotTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate));
                return (GetWindowLongW(window, -20) & 0x8) != 0;
            },
            isTopmost => isTopmost == topmost,
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
    }

    private static string DescribeWindowAt(IntPtr window, int x, int y)
    {
        GetWindowRect(window, out var rect);
        var exStyle = GetWindowLongW(window, -20);
        var atPoint = WindowFromPoint(new Point { X = x, Y = y });
        var root = atPoint == IntPtr.Zero ? IntPtr.Zero : GetAncestor(atPoint, 2);
        _ = GetWindowThreadProcessId(root, out var rootProcess);
        var className = new System.Text.StringBuilder(256);
        _ = GetClassNameW(root, className, className.Capacity);
        return $"target visible {IsWindowVisible(window)}, rect ({rect.Left}, {rect.Top})-({rect.Right}, {rect.Bottom}), topmost {(exStyle & 0x8) != 0}; " +
            $"window there 0x{root:X} class {className} process {rootProcess}";
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr window, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr window, System.Text.StringBuilder className, int maxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private void Run()
    {
        try
        {
            var bounds = Forms.Screen.PrimaryScreen!.WorkingArea;
            using var window = new TargetForm
            {
                Text = Title,
                StartPosition = Forms.FormStartPosition.Manual,
                Size = new System.Drawing.Size(Math.Min(900, bounds.Width - 100), Math.Min(320, bounds.Height - 100)),
            };

            // Lower half of the screen: Text Expander's question window opens a third of the way down.
            window.Location = new System.Drawing.Point(
                bounds.Left + ((bounds.Width - window.Width) / 2),
                bounds.Top + (bounds.Height * 3 / 5) - (window.Height / 2));
            using var box = new Forms.RichTextBox
            {
                AccessibleName = "Typed text",
                Dock = Forms.DockStyle.Fill,
                Multiline = true,
                AcceptsTab = true,
                DetectUrls = false,
                Font = new System.Drawing.Font("Segoe UI", 14),
            };
            editor = box;
            window.Controls.Add(box);
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

    /// <summary>Shown without activation: only <see cref="Focus"/>'s click brings it in front, as for a person.</summary>
    private sealed class TargetForm : Forms.Form
    {
        protected override bool ShowWithoutActivation => true;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
