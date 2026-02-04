// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Settings _settings;
        private readonly ResizeBatch _batch;

        [ObservableProperty]
        private object _currentPage;

        [ObservableProperty]
        private double _progress;

        public MainViewModel(ResizeBatch batch, Settings settings)
        {
            _batch = batch;
            _settings = settings;
        }

        [RelayCommand]
        public void Load(IMainView view)
        {
            if (_batch.Files.Count == 0)
            {
                foreach (var file in view.OpenPictureFiles())
                {
                    _batch.Files.Add(file);
                }
            }

            CurrentPage = new InputViewModel(_settings, this, view, _batch);
        }
    }
}
