#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Linq;
using ImageResizer.Properties;
using ImageResizer.ViewModels;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ImageResizer.Views
{
    public sealed partial class MainWindow : Window, IMainView
    {
        public MainWindow(MainViewModel viewModel)
        {
            this.InitializeComponent();

            // Set DataContext on root content
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = viewModel;
            }

            // Configure title bar
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.Title = ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer");

            // Size the window
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(360, 506));

            // Center on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var centerX = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                var centerY = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
            }

            // Load command
            this.Activated += OnActivated;
        }

        private bool _loaded;

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (!_loaded)
            {
                _loaded = true;
                if (this.Content is FrameworkElement rootElement && rootElement.DataContext is MainViewModel vm)
                {
                    vm.Load(this);
                }
            }
        }

        public IEnumerable<string> OpenPictureFiles()
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".jfif");
            picker.FileTypeFilter.Add(".jpe");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jxr");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");
            picker.FileTypeFilter.Add(".wdp");

            var files = picker.PickMultipleFilesAsync().AsTask().GetAwaiter().GetResult();

            if (files == null || files.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            return files.Select(f => f.Path);
        }

        void IMainView.Close()
        {
            this.DispatcherQueue.TryEnqueue(() => this.Close());
        }
    }
}
