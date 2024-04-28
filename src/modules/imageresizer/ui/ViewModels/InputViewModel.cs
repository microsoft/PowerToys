// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Common.UI;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : Observable
    {
        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;

        public enum Dimension
        {
            Width,
            Height,
        }

        public class KeyPressParams
        {
            public double Value { get; set; }

            public Dimension Dimension { get; set; }
        }

        public InputViewModel(
            Settings settings,
            MainViewModel mainViewModel,
            IMainView mainView,
            ResizeBatch batch)
        {
            _batch = batch;
            _mainViewModel = mainViewModel;
            _mainView = mainView;

            Settings = settings;
            if (settings != null)
            {
                settings.CustomSize.PropertyChanged += (sender, e) => settings.SelectedSize = (CustomSize)sender;
            }

            ResizeCommand = new RelayCommand(Resize);
            CancelCommand = new RelayCommand(Cancel);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            EnterKeyPressedCommand = new RelayCommand<KeyPressParams>(HandleEnterKeyPress);
        }

        public Settings Settings { get; }

        public IEnumerable<ResizeFit> ResizeFitValues => Enum.GetValues(typeof(ResizeFit)).Cast<ResizeFit>();

        public IEnumerable<ResizeUnit> ResizeUnitValues => Enum.GetValues(typeof(ResizeUnit)).Cast<ResizeUnit>();

        public ICommand ResizeCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand EnterKeyPressedCommand { get; private set; }

        // Any of the files is a gif
        public bool TryingToResizeGifFiles =>
                _batch.Files.Any(filename => filename.EndsWith(".gif", System.StringComparison.InvariantCultureIgnoreCase));

        public void Resize()
        {
            Settings.Save();
            _mainViewModel.CurrentPage = new ProgressViewModel(_batch, _mainViewModel, _mainView);
        }

        public static void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ImageResizer, false);
        }

        private void HandleEnterKeyPress(KeyPressParams parameters)
        {
            switch (parameters.Dimension)
            {
                case Dimension.Width:
                    Settings.CustomSize.Width = parameters.Value;
                    break;
                case Dimension.Height:
                    Settings.CustomSize.Height = parameters.Value;
                    break;
            }
        }

        public void Cancel()
            => _mainView.Close();
    }
}
