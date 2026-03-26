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
using WinUIEx;

namespace ImageResizer
{
    public sealed partial class MainWindow : WindowEx, IMainView
    {
        private const int MinWindowWidth = 400;

        private bool _isFirstShow = true;

        public MainViewModel ViewModel { get; }

        private PropertyChangedEventHandler _selectedSizeChangedHandler;
        private InputViewModel _currentInputViewModel;

        public MainWindow(MainViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            this.SetIcon("Assets/ImageResizer/ImageResizer.ico");

            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(this.GetWindowHandle());

            // Use a tall initial height so content is not clipped during first layout.
            // SizeToContent will shrink to the actual needed height after Loaded.
            this.SetWindowSize(MinWindowWidth, 800);

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

                AdjustWindowForInputPage(inputVM, inputPage);
            }
            else if (page is ProgressViewModel progressVM)
            {
                var progressPage = new ProgressPage { ViewModel = progressVM };
                contentPresenter.Content = progressPage;

                SizeAndShowOnLoaded(progressPage);
            }
            else if (page is ResultsViewModel resultsVM)
            {
                var resultsPage = new ResultsPage { ViewModel = resultsVM };
                contentPresenter.Content = resultsPage;

                SizeAndShowOnLoaded(resultsPage);
            }
        }

        /// <summary>
        /// After the element completes layout, size the window to fit and show it.
        /// </summary>
        private void SizeAndShowOnLoaded(FrameworkElement element)
        {
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                element.Loaded -= OnLoaded;
                SizeToContent();
                ShowWindow();
            }

            element.Loaded += OnLoaded;
        }

        private void AdjustWindowForInputPage(InputViewModel inputVM, InputPage inputPage)
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
                if (e.PropertyName == nameof(inputVM.Settings.SelectedSizeIndex))
                {
                    // Content visibility changes after the selected size option changes;
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
                ShowWindow();
            }

            inputPage.Loaded += OnLoaded;
        }

        /// <summary>
        /// Activate and center the window on first show; subsequent calls are no-ops.
        /// </summary>
        private void ShowWindow()
        {
            if (_isFirstShow)
            {
                _isFirstShow = false;
                this.CenterOnScreen();
                Activate();
            }
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
        /// Measure the root content at the current client width with unconstrained height,
        /// then resize the client area to match.
        /// </summary>
        private void SizeToContent()
        {
            var rootContent = this.Content as FrameworkElement;
            if (rootContent == null)
            {
                return;
            }

            var scale = this.GetDpiForWindow() / 96.0;
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

        public async Task<IEnumerable<string>> OpenPictureFilesAsync()
        {
            var picker = this.CreateOpenFilePicker();
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
