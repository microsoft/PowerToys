// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Hosts.Helpers;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace Hosts
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            InitializeComponent();

            SetTitleBar();

            BringToForeground();
        }

        private void SetTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
        }

        private void BringToForeground()
        {
            var handle = this.GetWindowHandle();
            var fgHandle = NativeMethods.GetForegroundWindow();

            var threadId1 = NativeMethods.GetWindowThreadProcessId(handle, System.IntPtr.Zero);
            var threadId2 = NativeMethods.GetWindowThreadProcessId(fgHandle, System.IntPtr.Zero);

            if (threadId1 != threadId2)
            {
                NativeMethods.AttachThreadInput(threadId1, threadId2, true);
                NativeMethods.SetForegroundWindow(handle);
                NativeMethods.AttachThreadInput(threadId1, threadId2, false);
            }
            else
            {
                NativeMethods.SetForegroundWindow(handle);
            }
        }
    }
}
