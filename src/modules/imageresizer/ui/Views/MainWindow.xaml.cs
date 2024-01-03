// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Linq;
using Common.UI;
using ImageResizer.ViewModels;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using AppResources = ImageResizer.Properties.Resources;

namespace ImageResizer.Views
{
    public partial class MainWindow : FluentWindow, IMainView
    {
        public MainWindow(MainViewModel viewModel)
        {
            DataContext = viewModel;

            InitializeComponent();

            if (OSVersionHelper.IsWindows11())
            {
                WindowBackdropType = WindowBackdropType.Mica;
            }
            else
            {
                WindowBackdropType = WindowBackdropType.None;
            }

            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType);
        }

        public IEnumerable<string> OpenPictureFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = AppResources.PictureFilter +
                    "|*.bmp;*.dib;*.exif;*.gif;*.jfif;*.jpe;*.jpeg;*.jpg;*.jxr;*.png;*.rle;*.tif;*.tiff;*.wdp|" +
                    AppResources.AllFilesFilter + "|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Multiselect = true,
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return Enumerable.Empty<string>();
            }

            return openFileDialog.FileNames;
        }

        void IMainView.Close()
            => Dispatcher.Invoke((Action)Close);
    }
}
