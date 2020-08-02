// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
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
            mainWindow.Show();

            // Temporary workaround for issue #1273
            BecomeForegroundWindow(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
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
