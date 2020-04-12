// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using GalaSoft.MvvmLight.Threading;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;

namespace ImageResizer
{
    public partial class App : Application
    {
        // Import the FindWindow API to find our window
        [DllImportAttribute("User32.dll")]
        private static extern int FindWindow(string className, string windowName);

        // Import the SetForeground API to activate it
        [DllImportAttribute("User32.dll")]
        private static extern IntPtr SetForegroundWindow(int hWnd);

        static App()
        {
            Console.InputEncoding = Encoding.Unicode;
            DispatcherHelper.Initialize();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var batch = ResizeBatch.FromCommandLine(Console.In, e.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));

            // Check process running
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).GetUpperBound(0) == 0)
            {
                mainWindow.Show();

                // Temporary workaround for issue #1273
                BecomeForegroundWindow(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
            }
            else
            {
                // The program has the open file dialog window and the image resizer window
                // Find the window, using the Window title
                // Check which window is running
                int hWndOpen = FindWindow(null, "Image Resizer - Open files");
                int hWndImageResizer = FindWindow(null, "Image Resizer");
                if (hWndOpen > 0)
                {
                    SetForegroundWindow(hWndOpen); // Activate Image Resizer - Open files window
                }

                if (hWndImageResizer > 0)
                {
                    SetForegroundWindow(hWndImageResizer); // Activate Image Resizer window
                }
            }
        }

        private void BecomeForegroundWindow(IntPtr hWnd)
        {
            Win32Helpers.INPUT input = new Win32Helpers.INPUT { type = Win32Helpers.INPUTTYPE.INPUT_MOUSE, data = { } };
            Win32Helpers.INPUT[] inputs = new Win32Helpers.INPUT[] { input };
            Win32Helpers.SendInput(1, inputs, Win32Helpers.INPUT.Size);
            Win32Helpers.SetForegroundWindow(hWnd);
        }
    }
}
