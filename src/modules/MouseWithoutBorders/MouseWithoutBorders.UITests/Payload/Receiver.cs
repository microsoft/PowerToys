// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Compiled by Windows PowerShell 5.1. The request channel cannot write text or mouse counters.
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Microsoft.MouseWithoutBorders.UITests
{
    // Keep the receiver's message pump responsive while PowerShell waits for
    // winapp: UIA desktop enumeration can also query this WinForms window.
    public sealed class ReceiverController : IDisposable
    {
        private readonly Thread thread;
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private Receiver receiver;
        private IntPtr handle;
        private Exception startupError;
        private volatile string startupStage = "Thread not scheduled";

        public ReceiverController(string role, string runId)
        {
            thread = new Thread(delegate()
            {
                try
                {
                    startupStage = "Enabling visual styles";
                    Application.EnableVisualStyles();
                    startupStage = "Constructing receiver";
                    receiver = new Receiver(role, runId);
                    startupStage = "Creating native window";
                    handle = receiver.Handle;
                    startupStage = "Running message pump";
                    ready.Set();
                    Application.Run(receiver);
                }
                catch (Exception error)
                {
                    startupError = error;
                    ready.Set();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "MWB receiver UI";
            thread.Start();
            if (!ready.WaitOne(TimeSpan.FromSeconds(60)))
            {
                throw new TimeoutException("The receiver UI thread did not initialize: " + startupStage + "; " + thread.ThreadState);
            }
            if (startupError != null)
            {
                throw new InvalidOperationException("Receiver initialization failed.", startupError);
            }
        }

        public IntPtr Handle { get { return handle; } }

        public string ReceivedText { get { return Invoke(delegate { return receiver.ReceivedText; }); } }

        public bool InputFocused { get { return Invoke(delegate { return receiver.InputFocused; }); } }

        public int Clicks { get { return Invoke(delegate { return receiver.Clicks; }); } }

        public Rectangle ClickBounds { get { return Invoke(delegate { return receiver.ClickBounds; }); } }

        public long ClickTargetHandle { get { return Invoke(delegate { return receiver.ClickTargetHandle; }); } }

        public int MouseDownMessages { get { return Invoke(delegate { return receiver.MouseDownMessages; }); } }

        public int MouseUpMessages { get { return Invoke(delegate { return receiver.MouseUpMessages; }); } }

        public void FocusInput() { Invoke(delegate { receiver.FocusInput(); return true; }); }

        public void ClearInput() { Invoke(delegate { receiver.ClearInput(); return true; }); }

        public string PublishClipboard() { return Invoke(delegate { return receiver.PublishClipboard(); }); }

        public string ClipboardDigest() { return Invoke(delegate { return receiver.ClipboardDigest(); }); }

        public void RestoreClipboard() { Invoke(delegate { receiver.RestoreClipboard(); return true; }); }

        public void CaptureDesktop(string path) { Invoke(delegate { receiver.CaptureDesktop(path); return true; }); }

        public void Close()
        {
            if (thread.IsAlive && receiver != null && !receiver.IsDisposed)
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
            ready.Dispose();
        }

        private T Invoke<T>(Func<T> action)
        {
            if (receiver == null || receiver.IsDisposed || !thread.IsAlive)
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
        private readonly DataObject originalClipboard;
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
            clickTarget.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left)
                {
                    Clicks++;
                }
            };
            Controls.Add(clickTarget);
            Controls.Add(input);
            Shown += delegate { input.Focus(); };
            if (role == "Host")
            {
                originalClipboard = new DataObject();
                IDataObject source = Clipboard.GetDataObject();
                if (source != null)
                {
                    foreach (string format in source.GetFormats(false))
                    {
                        object value = source.GetData(format, false);
                        MemoryStream memory = value as MemoryStream;
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

        public void FocusInput()
        {
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
            Clipboard.SetText(token);
            clipboardChanged = true;
            return ClipboardDigest();
        }

        public string ClipboardDigest()
        {
            string text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            if (!text.StartsWith("mwb-test-", StringComparison.Ordinal) ||
                text.IndexOf("-" + runId + "-", StringComparison.Ordinal) < 0)
            {
                // Do not fingerprint or publish a user's original clipboard in input observations.
                return string.Empty;
            }

            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
            }
        }

        public void RestoreClipboard()
        {
            if (originalClipboard != null && clipboardChanged)
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
    }
}
