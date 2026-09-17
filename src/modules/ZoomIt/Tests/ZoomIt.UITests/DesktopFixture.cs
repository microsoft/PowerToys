// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class DesktopFixture : IDisposable
{
    internal const int MarkerSize = 80;
    internal static readonly Color BackgroundColor = Color.FromArgb(32, 48, 64);
    internal static readonly Color MarkerColor = Color.Lime;

    private readonly Thread thread;
    private readonly TaskCompletionSource<Forms.Form> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Size screen = Forms.SystemInformation.PrimaryMonitorSize;
    private Forms.Form? form;
    private int disposeRequested;
    private Color markerColor = MarkerColor;
    private Forms.Timer? animation;
    private bool pulse;
    private int animationFrames;
    private Forms.TextBox? editor;
    private Forms.Timer? fontDialogDelay;

    internal DesktopFixture(Func<Forms.Form>? createWindow = null, TimeSpan? initializationTimeout = null)
    {
        thread = new Thread(() =>
        {
            try
            {
                using var window = (createWindow ?? (() => new Forms.Form()))();
                window.Text = "ZoomIt UI test source";
                window.FormBorderStyle = Forms.FormBorderStyle.None;
                window.StartPosition = Forms.FormStartPosition.Manual;
                window.Bounds = new Rectangle(Point.Empty, screen);
                window.BackColor = BackgroundColor;
                window.AutoScaleMode = Forms.AutoScaleMode.None;
                window.Paint += (_, e) =>
                {
                    using var brush = new SolidBrush(markerColor);
                    e.Graphics.FillRectangle(brush, (screen.Width - MarkerSize) / 2, (screen.Height - MarkerSize) / 2, MarkerSize, MarkerSize);
                    if (animation is not null)
                    {
                        using var pulseBrush = new SolidBrush(pulse ? Color.Cyan : Color.Magenta);
                        e.Graphics.FillRectangle(pulseBrush, 10, 10, 64, 64);
                        Interlocked.Increment(ref animationFrames);
                    }
                };
                window.FormClosed += (_, _) =>
                {
                    animation?.Dispose();
                    fontDialogDelay?.Dispose();
                };
                window.Shown += (_, _) =>
                {
                    ready.TrySetResult(window);
                    if (Volatile.Read(ref disposeRequested) != 0)
                    {
                        window.Close();
                    }
                };

                // A timed-out constructor can request disposal before the HWND exists.
                if (Volatile.Read(ref disposeRequested) == 0)
                {
                    Forms.Application.Run(window);
                }
            }
            catch (Exception exception)
            {
                if (!ready.TrySetException(exception))
                {
                    throw;
                }
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        try
        {
            thread.Start();
            form = ready.Task.WaitAsync(initializationTimeout ?? TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            Show();
            Assert.IsTrue(
                WaitHelper.WaitForStable(ReadMarkerWidth, width => Math.Abs(width - MarkerSize) <= 2, 5_000, 2).Succeeded,
                "The unzoomed source must render an 80-pixel green marker before testing magnification.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal Point Center => new(screen.Width / 2, screen.Height / 2);

    private Forms.Form Window => form ?? throw new InvalidOperationException("The source window was not initialized.");

    internal void Show()
    {
        var window = Window;
        window.Invoke(() =>
        {
            window.Show();
            window.Activate();
        });
        var focused = WindowControl.WaitForForeground(window.Handle, 2_000);
        Point[] activationPoints = [Center, new(80, 80), new(screen.Width - 80, 80)];
        foreach (var point in activationPoints)
        {
            if (focused)
            {
                break;
            }

            if (WindowControl.IsPointOwnedByWindow(window.Handle, point.X, point.Y))
            {
                MouseHelper.LeftClickAt(point.X, point.Y);
                focused = WindowControl.WaitForForeground(window.Handle, 10_000);
            }
        }

        Assert.IsTrue(
            focused,
            $"Source window did not acquire foreground: {WindowControl.GetForegroundWindowInfo()}");
        MouseHelper.MoveTo(Center.X, Center.Y);
    }

    internal void ChangeMarker(Color color)
    {
        Window.Invoke(() =>
        {
            markerColor = color;
            Window.Refresh();
        });
    }

    internal void StartAnimation()
    {
        Window.Invoke(() =>
        {
            animation = new Forms.Timer { Interval = 100 };
            animation.Tick += (_, _) =>
            {
                pulse = !pulse;
                Window.Invalidate(new Rectangle(10, 10, 64, 64));
            };
            animation.Start();
        });
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => Volatile.Read(ref animationFrames), frames => frames >= 3, 5_000).Succeeded,
            "The recording source did not produce animated frames.");
    }

    internal void ShowEditor()
    {
        Window.Invoke(() =>
        {
            editor = new Forms.TextBox { Multiline = true, AcceptsTab = true, Dock = Forms.DockStyle.Fill };
            Window.Controls.Add(editor);
        });
        FocusEditor();
    }

    internal void ShowFontDialogAfterDelay(int delayMS)
    {
        Window.Invoke(() =>
        {
            fontDialogDelay = new Forms.Timer { Interval = delayMS };
            fontDialogDelay.Tick += (_, _) =>
            {
                fontDialogDelay.Stop();
                using var dialog = new Forms.FontDialog();
                dialog.ShowDialog(Window);
            };
            fontDialogDelay.Start();
        });
    }

    internal void FocusEditor()
    {
        Show();
        Window.Invoke(() => (editor ?? throw new InvalidOperationException("The editor fixture was not initialized.")).Focus());
    }

    internal void ClearEditor() => Window.Invoke(() =>
    {
        (editor ?? throw new InvalidOperationException("The editor fixture was not initialized.")).Clear();
    });

    internal string ReadEditorText() => Window.Invoke(() =>
        (editor ?? throw new InvalidOperationException("The editor fixture was not initialized.")).Text);

    internal Bitmap? ReadClipboardImage() => (Bitmap?)Window.Invoke(() =>
    {
        using var image = Forms.Clipboard.GetImage();
        return image is null ? null : new Bitmap(image);
    });

    internal static Bitmap Capture()
    {
        var size = Forms.SystemInformation.PrimaryMonitorSize;
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(Point.Empty, Point.Empty, size);
        return bitmap;
    }

    internal int ReadMarkerWidth()
    {
        using var bitmap = Capture();
        using var pixels = new BitmapPixels(bitmap, new Rectangle(0, Center.Y - 10, bitmap.Width, 1));
        var width = 0;
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (IsColor(pixels.GetColor(x, 0), MarkerColor))
            {
                width++;
            }
        }

        return width;
    }

    internal static bool IsColor(Color actual, Color expected, int tolerance = 10) =>
        Math.Abs(actual.R - expected.R) <= tolerance &&
        Math.Abs(actual.G - expected.G) <= tolerance &&
        Math.Abs(actual.B - expected.B) <= tolerance;

    internal static Rectangle ColorBounds(Bitmap bitmap, Func<Color, bool> predicate, Rectangle? region = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var area = Rectangle.Intersect(region ?? new Rectangle(Point.Empty, bitmap.Size), new Rectangle(Point.Empty, bitmap.Size));
        if (area.Width <= 0 || area.Height <= 0)
        {
            return Rectangle.Empty;
        }

        using var pixels = new BitmapPixels(bitmap, area);
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = -1;
        var bottom = -1;
        for (var y = area.Top; y < area.Bottom; y += 2)
        {
            for (var x = area.Left; x < area.Right; x += 2)
            {
                if (predicate(pixels.GetColor(x - area.Left, y - area.Top)))
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeRequested, 1) != 0)
        {
            return;
        }

        var window = form ?? (ready.Task.IsCompletedSuccessfully ? ready.Task.Result : null);
        try
        {
            if (window is not null && !window.IsDisposed)
            {
                Assert.IsTrue(WindowControl.TryCloseWindow(window.Handle.ToInt64()), "The test source window did not close.");
            }
        }
        finally
        {
            Assert.IsTrue(!thread.IsAlive || thread.Join(TimeSpan.FromSeconds(10)), "The test source's UI thread did not exit.");
        }
    }
}
