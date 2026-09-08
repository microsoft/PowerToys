// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CropAndLock.TestApp;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.CropAndLock.UITests
{
    internal sealed class Win32CropSource : CropSource
    {
        private Thread? thread;
        private Exception? threadError;

        internal override void Open(TestContext context)
        {
            Assert.IsNull(NativeMethods.PackageFullName(Environment.ProcessId), "The Win32 fixture must be unpackaged.");
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            thread = new Thread(() =>
            {
                try
                {
                    using var form = new CropSourceForm();
                    form.Shown += (_, _) =>
                    {
                        Window = form.Handle;
                        SetGeometry(CropSourceForm.CropRectangle, CropSourceForm.InputRectangle);
                        ready.TrySetResult();
                    };
                    Forms.Application.Run(form);
                }
                catch (Exception error)
                {
                    // Marshal UI-thread failures back to the test rather than losing the fixture's diagnostic.
                    threadError = error;
                    ready.TrySetException(error);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
            context.WriteLine($"Win32 source: HWND=0x{Window:X}, PID={Environment.ProcessId}, package=<none>.");
        }

        public override void Dispose()
        {
            if (Window != IntPtr.Zero)
            {
                Assert.IsTrue(WindowControl.TryCloseWindow(Window.ToInt64()), "The test-owned source window did not close.");
            }

            if (thread is not null)
            {
                Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)), "The Win32 fixture's UI thread did not exit.");
            }

            Assert.IsNull(threadError, $"The Win32 fixture failed: {threadError}");
        }
    }
}
