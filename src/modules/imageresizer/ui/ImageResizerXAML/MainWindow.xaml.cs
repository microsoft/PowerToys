// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Graphics;
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

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.SetIcon("Assets/ImageResizer/ImageResizer.ico");
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());

            // Center the window on screen
            this.CenterOnScreen();

            // Listen to ViewModel property changes
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        public async Task LoadViewModelAsync()
        {
            await ViewModel.LoadAsync(this);
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

            // Size immediately so the window is correct before Activate
            SizeToContent();
            this.CenterOnScreen();
        }

        /// <summary>
        /// WinUI3 has no built-in SizeToContent (unlike WPF).
        /// Measure content, then use ResizeClient to set the client area directly.
        /// </summary>
        private void SizeToContent()
        {
            var content = contentPresenter.Content as FrameworkElement;
            if (content == null)
            {
                return;
            }

            content.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredHeight = content.DesiredSize.Height;

            if (desiredHeight <= 0)
            {
                return;
            }

            var scale = this.GetDpiForWindow() / 96.0;
            var clientWidth = AppWindow.ClientSize.Width;
            var clientHeight = (int)Math.Ceiling(desiredHeight * scale);

            AppWindow.ResizeClient(new SizeInt32(clientWidth, clientHeight));
        }

        public async Task<IEnumerable<string>> OpenPictureFilesAsync()
        {
            var picker = new FileOpenPicker();

            // Initialize the picker with the window handle
            var hwnd = this.GetWindowHandle();
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

            var files = await picker.PickMultipleFilesAsync();
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
