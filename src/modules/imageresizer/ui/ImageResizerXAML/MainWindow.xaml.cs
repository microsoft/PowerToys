// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ImageResizer.Helpers;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;

namespace ImageResizer
{
    public sealed partial class MainWindow : WindowEx, IMainView
    {
        public MainViewModel ViewModel { get; }

        private PropertyChangedEventHandler _selectedSizeChangedHandler;
        private InputViewModel _currentInputViewModel;

        // Window chrome height (title bar)
        private const double WindowChromeHeight = 32;

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            var loader = ResourceLoaderInstance.ResourceLoader;
            var title = loader.GetString("ImageResizer");
            Title = title;

            // Center the window on screen
            this.CenterOnScreen();

            // Set window icon
            try
            {
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "ImageResizer", "ImageResizer.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.SetIcon(iconPath);  // WinUIEx extension method
                }
            }
            catch
            {
                // Icon loading failed, continue without icon
            }

            // Add Mica backdrop on Windows 11
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            }

            // Listen to ViewModel property changes
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Load the ViewModel after window is ready
            ViewModel.Load(this);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentPage))
            {
                UpdateCurrentPage();
            }
        }

        private void UpdateCurrentPage()
        {
            var page = ViewModel.CurrentPage;
            if (page == null)
            {
                contentPresenter.Content = null;
                return;
            }

            if (page is InputViewModel inputVM)
            {
                var inputPage = new InputPage { ViewModel = inputVM, DataContext = inputVM };
                contentPresenter.Content = inputPage;

                // Adjust window height based on selected size type
                AdjustWindowHeightForInputPage(inputVM);
            }
            else if (page is ProgressViewModel progressVM)
            {
                var progressPage = new ProgressPage { ViewModel = progressVM, DataContext = progressVM };
                contentPresenter.Content = progressPage;

                // Size to content after layout
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SizeToContent());
            }
            else if (page is ResultsViewModel resultsVM)
            {
                var resultsPage = new ResultsPage { ViewModel = resultsVM, DataContext = resultsVM };
                contentPresenter.Content = resultsPage;

                // Size to content after layout
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SizeToContent());
            }
        }

        private void AdjustWindowHeightForInputPage(InputViewModel inputVM)
        {
            // Unsubscribe previous handler to prevent memory leak
            if (_selectedSizeChangedHandler != null && _currentInputViewModel?.Settings != null)
            {
                _currentInputViewModel.Settings.PropertyChanged -= _selectedSizeChangedHandler;
            }

            _currentInputViewModel = inputVM;

            // Create and store handler reference for future cleanup
            _selectedSizeChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(inputVM.Settings.SelectedSize))
                {
                    // Delay to allow layout to update
                    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SizeToContent());
                }
            };

            inputVM.Settings.PropertyChanged += _selectedSizeChangedHandler;

            // Set initial height after layout
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SizeToContent());
        }

        private void SizeToContent()
        {
            // Get the content element
            var content = contentPresenter.Content as FrameworkElement;
            if (content == null)
            {
                return;
            }

            // Measure the content to get its desired size
            content.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredHeight = content.DesiredSize.Height;

            if (desiredHeight <= 0)
            {
                return;
            }

            // Add window chrome height and extra padding for safety
            var totalHeight = desiredHeight + WindowChromeHeight + 16;

            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                var scaleFactor = Content?.XamlRoot?.RasterizationScale ?? 1.0;
                var currentSize = appWindow.Size;
                var newHeightInPixels = (int)(totalHeight * scaleFactor);
                appWindow.Resize(new Windows.Graphics.SizeInt32(currentSize.Width, newHeightInPixels));
            }
        }

        public IEnumerable<string> OpenPictureFiles()
        {
            var picker = new FileOpenPicker();

            // Initialize the picker with the window handle
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".dib");
            picker.FileTypeFilter.Add(".exif");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".jfif");
            picker.FileTypeFilter.Add(".jpe");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jxr");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".rle");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");
            picker.FileTypeFilter.Add(".wdp");

            var files = picker.PickMultipleFilesAsync().AsTask().GetAwaiter().GetResult();
            if (files != null && files.Count > 0)
            {
                return files.Select(f => f.Path);
            }

            return Enumerable.Empty<string>();
        }

        void IMainView.Close()
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, Close);
        }
    }
}
