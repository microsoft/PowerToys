// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ImageResizer
{
    public sealed partial class MainWindow : Window, IMainView
    {
        private const int InitialWidth = 400;
        private const int InitialHeight = 506;

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

            // Configure window to be non-resizable with no minimize/maximize buttons
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(hwnd);

            // Set initial size and center on screen
            var scale = NativeMethods.GetDpiForWindow(hwnd) / 96.0;
            AppWindow.Resize(new SizeInt32((int)(InitialWidth * scale), (int)(InitialHeight * scale)));
            CenterOnScreen();

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
                var inputPage = new InputPage { ViewModel = inputVM };
                contentPresenter.Content = inputPage;

                AdjustWindowHeightForInputPage(inputVM, inputPage);
            }
            else if (page is ProgressViewModel progressVM)
            {
                var progressPage = new ProgressPage { ViewModel = progressVM };
                contentPresenter.Content = progressPage;

                SizeToContentOnLoaded(progressPage);
            }
            else if (page is ResultsViewModel resultsVM)
            {
                var resultsPage = new ResultsPage { ViewModel = resultsVM };
                contentPresenter.Content = resultsPage;

                SizeToContentOnLoaded(resultsPage);
            }
        }

        /// <summary>
        /// Sizes the window to fit content once the element has completed layout.
        /// </summary>
        private void SizeToContentOnLoaded(FrameworkElement element)
        {
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                element.Loaded -= OnLoaded;
                SizeToContent();
            }

            element.Loaded += OnLoaded;
        }

        private void AdjustWindowHeightForInputPage(InputViewModel inputVM, InputPage inputPage)
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
                    // Content visibility changes after SelectedSize changes;
                    // listen for the next LayoutUpdated to re-measure once layout settles.
                    SizeToContentAfterLayout(inputPage);
                }
            };

            inputVM.Settings.PropertyChanged += _selectedSizeChangedHandler;

            // Size after initial layout completes (bindings resolved, content measured)
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                inputPage.Loaded -= OnLoaded;
                SizeToContent();
                CenterOnScreen();
            }

            inputPage.Loaded += OnLoaded;
        }

        /// <summary>
        /// Sizes the window after the next layout pass completes.
        /// Used when content changes on an already-loaded element (e.g., visibility toggles).
        /// LayoutUpdated fires once per layout pass, so we unsubscribe immediately.
        /// </summary>
        private void SizeToContentAfterLayout(FrameworkElement element)
        {
            void OnLayoutUpdated(object sender, object e)
            {
                element.LayoutUpdated -= OnLayoutUpdated;
                SizeToContent();
            }

            element.LayoutUpdated += OnLayoutUpdated;
        }

        /// <summary>
        /// WinUI3 has no built-in SizeToContent (unlike WPF).
        /// Measure the root content (including TitleBar), then use ResizeClient to set the client area directly.
        /// </summary>
        private void SizeToContent()
        {
            var rootContent = this.Content as FrameworkElement;
            if (rootContent == null)
            {
                return;
            }

            var scale = NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
            var clientWidth = AppWindow.ClientSize.Width;
            var availableWidth = clientWidth / scale;

            rootContent.Measure(new Windows.Foundation.Size(availableWidth, double.PositiveInfinity));
            var desiredHeight = rootContent.DesiredSize.Height;

            if (desiredHeight <= 0)
            {
                return;
            }

            var clientHeight = (int)Math.Ceiling(desiredHeight * scale);

            AppWindow.ResizeClient(new SizeInt32(clientWidth, clientHeight));
        }

        /// <summary>
        /// Centers the window on the nearest display area.
        /// </summary>
        private void CenterOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var windowSize = AppWindow.Size;
                var centeredPosition = new PointInt32
                {
                    X = (displayArea.WorkArea.Width - windowSize.Width) / 2,
                    Y = (displayArea.WorkArea.Height - windowSize.Height) / 2,
                };
                AppWindow.Move(centeredPosition);
            }
        }

        public async Task<IEnumerable<string>> OpenPictureFilesAsync()
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

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                return files.Select(f => f.Path);
            }

            return [];
        }

        void IMainView.Close()
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, Close);
        }
    }
}
