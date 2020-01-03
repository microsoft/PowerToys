// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Text;
using System.Windows;
using GalaSoft.MvvmLight.Threading;
using ImageResizer.Models;
using ImageResizer.Properties;
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
            new MainWindow(new MainViewModel(batch, Settings.Default)).Show();
        }
    }
}
