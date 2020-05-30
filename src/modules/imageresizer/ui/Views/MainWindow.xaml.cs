// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ImageResizer.ViewModels;
using Microsoft.Win32;
using AppResources = ImageResizer.Properties.Resources;

namespace ImageResizer.Views
{
    public partial class MainWindow : Window, IMainView
    {
        public MainWindow(MainViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
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

        public void ShowAdvanced(AdvancedViewModel viewModel)
            => viewModel.Close(new AdvancedWindow(viewModel).ShowDialog() == true);

        void IMainView.Close()
            => Dispatcher.Invoke((Action)Close);
    }
}
