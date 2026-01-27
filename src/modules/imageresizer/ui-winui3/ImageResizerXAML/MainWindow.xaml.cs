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
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;

namespace ImageResizer
{
    public sealed partial class MainWindow : WindowEx, IMainView
    {
        public MainViewModel ViewModel { get; }

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            var loader = ResourceLoaderInstance.ResourceLoader;
            var title = loader.GetString("ImageResizer");
            Title = title;

            // Center the window on screen
            this.CenterOnScreen();

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
            }
            else if (page is ProgressViewModel progressVM)
            {
                var progressPage = new ProgressPage { ViewModel = progressVM, DataContext = progressVM };
                contentPresenter.Content = progressPage;
            }
            else if (page is ResultsViewModel resultsVM)
            {
                var resultsPage = new ResultsPage { ViewModel = resultsVM, DataContext = resultsVM };
                contentPresenter.Content = resultsPage;
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
