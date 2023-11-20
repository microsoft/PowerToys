// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using System.Windows.Input;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class MainViewModel : Observable
    {
        private readonly Settings _settings;
        private readonly ResizeBatch _batch;

        private object _currentPage;
        private double _progress;

        public MainViewModel(ResizeBatch batch, Settings settings)
        {
            _batch = batch;
            _settings = settings;
            LoadCommand = new RelayCommand<IMainView>(Load);
        }

        public ICommand LoadCommand { get; }

        public object CurrentPage
        {
            get => _currentPage;
            set => Set(ref _currentPage, value);
        }

        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        public void Load(IMainView view)
        {
            if (_batch.Files.Count == 0)
            {
                _batch.Files.AddRange(view.OpenPictureFiles());
            }

            CurrentPage = new InputViewModel(_settings, this, view, _batch);
        }
    }
}
