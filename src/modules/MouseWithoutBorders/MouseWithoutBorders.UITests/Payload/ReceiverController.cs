// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Compiled by Windows PowerShell 5.1. Keep this file C# 5 compatible.
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Microsoft.MouseWithoutBorders.UITests
{
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
            thread = new Thread(() =>
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

        public IntPtr Handle
        {
            get { return handle; }
        }

        public string ReceivedText
        {
            get { return Invoke(() => receiver.ReceivedText); }
        }

        public bool InputFocused
        {
            get { return Invoke(() => receiver.InputFocused); }
        }

        public int Clicks
        {
            get { return Invoke(() => receiver.Clicks); }
        }

        public int StartMenuDismissals
        {
            get { return Invoke(() => receiver.StartMenuDismissals); }
        }

        public Rectangle ClickBounds
        {
            get { return Invoke(() => receiver.ClickBounds); }
        }

        public long ClickTargetHandle
        {
            get { return Invoke(() => receiver.ClickTargetHandle); }
        }

        public int MouseDownMessages
        {
            get { return Invoke(() => receiver.MouseDownMessages); }
        }

        public int MouseUpMessages
        {
            get { return Invoke(() => receiver.MouseUpMessages); }
        }

        public void FocusInput()
        {
            Invoke(() =>
            {
                receiver.FocusInput();
                return true;
            });
        }

        public void ClearInput()
        {
            Invoke(() =>
            {
                receiver.ClearInput();
                return true;
            });
        }

        public string PublishClipboard()
        {
            return Invoke(() => receiver.PublishClipboard());
        }

        public string ClipboardDigest()
        {
            return Invoke(() => receiver.ClipboardDigest());
        }

        public ClipboardObservation ObserveClipboard()
        {
            return Invoke(() => receiver.ObserveClipboard());
        }

        public void RestoreClipboard()
        {
            Invoke(() =>
            {
                receiver.RestoreClipboard();
                return true;
            });
        }

        public void CaptureDesktop(string path)
        {
            Invoke(() =>
            {
                receiver.CaptureDesktop(path);
                return true;
            });
        }

        public void CaptureFailureDesktop(string path)
        {
            Receiver.CaptureFailureDesktop(path);
        }

        public void Close()
        {
            if (thread.IsAlive && !receiver.IsDisposed)
            {
                Invoke(() =>
                {
                    receiver.Close();
                    return true;
                });
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
}
