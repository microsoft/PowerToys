// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Compiled by Windows PowerShell 5.1. The request channel cannot write text or mouse counters.
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Microsoft.MouseWithoutBorders.UITests
{
    public sealed class ClipboardObservation
    {
        private ClipboardObservation(string digest, int? errorHResult)
        {
            Digest = digest;
            ErrorHResult = errorHResult;
        }

        // Empty when the clipboard is unavailable; check Available before using it.
        public string Digest { get; private set; }

        public int? ErrorHResult { get; private set; }

        public bool Available { get { return !ErrorHResult.HasValue; } }

        internal static ClipboardObservation Read(Func<string> readDigest)
        {
            try
            {
                return new ClipboardObservation(readDigest(), null);
            }
            catch (ExternalException error)
            {
                if (error.ErrorCode != unchecked((int)0x800401D0))
                {
                    throw;
                }

                return new ClipboardObservation(string.Empty, error.ErrorCode);
            }
        }
    }

    // Keep the receiver's message pump responsive while PowerShell waits for
    // winapp: UIA desktop enumeration can also query this WinForms window.
    public sealed class ReceiverController : IDisposable
    {
        private readonly Thread thread;
        private readonly Receiver receiver;
        private IntPtr handle;
        private volatile string startupStage = "Thread not scheduled";

        public ReceiverController(string role, string runId)
        {
            var started = new TaskCompletionSource<Receiver>();
            thread = new Thread(delegate()
            {
                try
                {
                    startupStage = "Enabling visual styles";
                    Application.EnableVisualStyles();
                    startupStage = "Constructing receiver";
                    var created = new Receiver(role, runId);
                    startupStage = "Creating native window";
                    handle = created.Handle;
                    startupStage = "Running message pump";
                    started.TrySetResult(created);
                    Application.Run(created);
                }
                catch (Exception error)
                {
                    started.TrySetException(error);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "MWB receiver UI";
            thread.Start();
            try
            {
                if (!started.Task.Wait(TimeSpan.FromSeconds(60)))
                {
                    throw new TimeoutException("The receiver UI thread did not initialize: " + startupStage + "; " + thread.ThreadState);
                }
            }
            catch (AggregateException error)
            {
                throw new InvalidOperationException("Receiver initialization failed.", error.InnerException);
            }

            receiver = started.Task.Result;
        }

        public IntPtr Handle { get { return handle; } }

        public string ReceivedText { get { return Invoke(delegate { return receiver.ReceivedText; }); } }

        public bool InputFocused { get { return Invoke(delegate { return receiver.InputFocused; }); } }

        public int Clicks { get { return Invoke(delegate { return receiver.Clicks; }); } }

        public int StartMenuDismissals { get { return Invoke(delegate { return receiver.StartMenuDismissals; }); } }

        public Rectangle ClickBounds { get { return Invoke(delegate { return receiver.ClickBounds; }); } }

        public long ClickTargetHandle { get { return Invoke(delegate { return receiver.ClickTargetHandle; }); } }

        public int MouseDownMessages { get { return Invoke(delegate { return receiver.MouseDownMessages; }); } }

        public int MouseUpMessages { get { return Invoke(delegate { return receiver.MouseUpMessages; }); } }

        public void FocusInput() { Invoke(delegate { receiver.FocusInput(); return true; }); }

        public void ClearInput() { Invoke(delegate { receiver.ClearInput(); return true; }); }

        public string PublishClipboard() { return Invoke(delegate { return receiver.PublishClipboard(); }); }

        public string ClipboardDigest() { return Invoke(delegate { return receiver.ClipboardDigest(); }); }

        public ClipboardObservation ObserveClipboard() { return Invoke(delegate { return receiver.ObserveClipboard(); }); }

        public void RestoreClipboard() { Invoke(delegate { receiver.RestoreClipboard(); return true; }); }

        public void CaptureDesktop(string path) { Invoke(delegate { receiver.CaptureDesktop(path); return true; }); }

        public void CaptureFailureDesktop(string path) { Receiver.CaptureFailureDesktop(path); }

        public void Close()
        {
            if (thread.IsAlive && !receiver.IsDisposed)
            {
                Invoke(delegate { receiver.Close(); return true; });
            }
        }

        public void Dispose()
        {
            Close();
            if (!thread.Join(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Receiver UI thread did not stop.");
            }
        }

        private T Invoke<T>(Func<T> action)
        {
            if (receiver.IsDisposed || !thread.IsAlive)
            {
                throw new InvalidOperationException("Receiver UI is no longer available.");
            }
            return (T)receiver.Invoke(action);
        }
    }

    public sealed class Receiver : Form
    {
        private sealed class ClickPanel : Panel
        {
            public int MouseDownMessages { get; private set; }

            public int MouseUpMessages { get; private set; }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0201) { MouseDownMessages++; }
                if (message.Msg == 0x0202) { MouseUpMessages++; }
                base.WndProc(ref message);
            }
        }

        private readonly TextBox input;
        private readonly ClickPanel clickTarget;
        private readonly DataObject originalClipboard = new DataObject();
        private readonly bool restoresClipboard;
        private readonly string role;
        private readonly string runId;
        private bool clipboardChanged;

        public Receiver(string role, string runId)
        {
            this.role = role;
            this.runId = runId;
            Text = "MWB " + role + " receiver " + runId;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(800, 500);
            WindowState = FormWindowState.Maximized;
            input = new TextBox
            {
                Name = "InputReceiver",
                AccessibleName = "MWB input receiver",
                Multiline = true,
                Dock = DockStyle.Top,
                Height = 150,
                Font = new Font("Consolas", 22),
            };
            clickTarget = new ClickPanel
            {
                Name = "ClickTarget",
                AccessibleName = "MWB mouse target",
                Dock = DockStyle.Fill,
                BackColor = Color.LightSteelBlue,
            };
            // A MouseDown is physical input. Unlike Button.Click, Space/Invoke cannot increment it.
            clickTarget.MouseDown += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    Clicks++;
                }
            };
            Controls.Add(clickTarget);
            Controls.Add(input);
            Shown += delegate { input.Focus(); };
            restoresClipboard = role == "Host";
            if (restoresClipboard)
            {
                var source = Clipboard.GetDataObject();
                if (source != null)
                {
                    foreach (string format in source.GetFormats(false))
                    {
                        var value = source.GetData(format, false);
                        var memory = value as MemoryStream;
                        if (memory != null)
                        {
                            value = new MemoryStream(memory.ToArray());
                        }

                        if (value != null)
                        {
                            originalClipboard.SetData(format, false, value);
                        }
                    }
                }
            }
        }

        public int Clicks { get; private set; }

        public long ClickTargetHandle { get { return clickTarget.Handle.ToInt64(); } }

        public int MouseDownMessages { get { return clickTarget.MouseDownMessages; } }

        public int MouseUpMessages { get { return clickTarget.MouseUpMessages; } }

        public string ReceivedText
        {
            get { return input.Text; }
        }

        public Rectangle ClickBounds
        {
            get { return clickTarget.RectangleToScreen(clickTarget.ClientRectangle); }
        }

        public bool InputFocused
        {
            get { return input.ContainsFocus; }
        }

        public int StartMenuDismissals { get; private set; }

        public void FocusInput()
        {
            if (NativeSupport.DismissForegroundStartMenu(delegate { SendKeys.SendWait("{ESC}"); }))
            {
                StartMenuDismissals++;
            }

            NativeSupport.ShowWindow(Handle, 3);
            NativeSupport.FocusWindow(Handle);
            Activate();
            input.Focus();
        }

        public void ClearInput()
        {
            input.Clear();
        }

        public string PublishClipboard()
        {
            // This source token is never sent to the other endpoint's request channel.
            string token = "mwb-test-" + role + "-" + runId + "-" + Guid.NewGuid().ToString("N");
            DataObject data = new DataObject();
            data.SetText(token, TextDataFormat.UnicodeText);
            clipboardChanged = true;
            // Keep the same token while bounded OLE retries cover slow clipboard owners.
            Clipboard.SetDataObject(data, true, 150, 100);
            // An immediate OLE clipboard read can still be empty on a cold desktop.
            // Acknowledge the generated value; the controller verifies its publication.
            return Digest(token);
        }

        public string ClipboardDigest()
        {
            string text = ReadClipboardText();
            if (!text.StartsWith("mwb-test-", StringComparison.Ordinal) ||
                text.IndexOf("-" + runId + "-", StringComparison.Ordinal) < 0)
            {
                // Do not fingerprint or publish a user's original clipboard in input observations.
                return string.Empty;
            }

            return Digest(text);
        }

        public ClipboardObservation ObserveClipboard()
        {
            return ClipboardObservation.Read(ClipboardDigest);
        }

        private string ReadClipboardText()
        {
            if (!OpenClipboard(Handle))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 5)
                {
                    // Report contention as CLIPBRD_E_CANT_OPEN, like the WinForms clipboard APIs.
                    Marshal.ThrowExceptionForHR(unchecked((int)0x800401D0), new IntPtr(-1));
                }

                throw new Win32Exception(error, "OpenClipboard failed.");
            }

            string text;
            bool closed;
            int closeError = 0;
            try
            {
                text = ReadOpenClipboardText();
            }
            finally
            {
                closed = CloseClipboard();
                if (!closed)
                {
                    closeError = Marshal.GetLastWin32Error();
                }
            }

            if (!closed)
            {
                throw new Win32Exception(closeError, "CloseClipboard failed.");
            }

            return text;
        }

        private static string ReadOpenClipboardText()
        {
            const uint unicodeText = 13;
            if (!IsClipboardFormatAvailable(unicodeText))
            {
                return string.Empty;
            }

            IntPtr data = GetClipboardData(unicodeText);
            if (data == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetClipboardData failed.");
            }

            ulong bytes = GlobalSize(data).ToUInt64();
            if (bytes == 0 || bytes % 2 != 0 || bytes / 2 > int.MaxValue)
            {
                throw new InvalidOperationException("The Unicode clipboard allocation has an invalid size.");
            }

            IntPtr memory = GlobalLock(data);
            if (memory == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalLock failed for clipboard data.");
            }

            string text;
            int unlockError = 0;
            try
            {
                text = Marshal.PtrToStringUni(memory, (int)(bytes / 2));
            }
            finally
            {
                SetNativeLastError(0);
                if (!GlobalUnlock(data))
                {
                    unlockError = Marshal.GetLastWin32Error();
                }
            }

            if (unlockError != 0)
            {
                throw new Win32Exception(unlockError, "GlobalUnlock failed for clipboard data.");
            }

            int terminator = text.IndexOf('\0');
            if (terminator < 0)
            {
                throw new InvalidOperationException("The Unicode clipboard data is not terminated.");
            }

            return text.Substring(0, terminator);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr owner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr GlobalSize(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        private static extern void SetNativeLastError(uint error);

        [SuppressMessage("Performance", "CA1850", Justification = "Windows PowerShell 5.1 uses .NET Framework without SHA256.HashData.")]
        [SuppressMessage("Performance", "CA1872", Justification = "Windows PowerShell 5.1 uses .NET Framework without Convert.ToHexString.")]
        private static string Digest(string text)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
            }
        }

        public void RestoreClipboard()
        {
            if (restoresClipboard && clipboardChanged)
            {
                Clipboard.SetDataObject(originalClipboard, true);
                clipboardChanged = false;
            }
        }

        public void CaptureDesktop(string path)
        {
            Refresh();
            // Diagnostic snapshot of this test-owned control only. Never include
            // another window's generated pairing key or original clipboard.
            using (Bitmap bitmap = new Bitmap(Width, Height))
            {
                DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static void CaptureFailureDesktop(string path)
        {
            if (!NativeSupport.Desktop().Ready)
            {
                throw new InvalidOperationException("Failure capture requires the active test desktop.");
            }
            Rectangle bounds = SystemInformation.VirtualScreen;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                // Private failure media, like the existing recording, may show disposable pairing keys.
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
