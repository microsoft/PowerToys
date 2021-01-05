// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Text;
using System.Windows;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using Microsoft.PowerToys.Common.UI;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        private ThemeManager _themeManager;
        private bool _isDisposed;

        static App()
        {
            Console.InputEncoding = Encoding.Unicode;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();

            _themeManager = new ThemeManager(this);

            // Temporary workaround for issue #1273
            BecomeForegroundWindow(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
        }

        private static void BecomeForegroundWindow(IntPtr hWnd)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT { type = NativeMethods.INPUTTYPE.INPUT_MOUSE, data = { } };
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);
            NativeMethods.SetForegroundWindow(hWnd);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _themeManager?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
