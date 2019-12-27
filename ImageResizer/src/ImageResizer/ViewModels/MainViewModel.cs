using System.Collections.Generic;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        readonly Settings _settings;
        readonly ResizeBatch _batch;

        object _currentPage;
        double _progress;

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
            set => Set(nameof(CurrentPage), ref _currentPage, value);
        }

        public double Progress
        {
            get => _progress;
            set => Set(nameof(Progress), ref _progress, value);
        }

        public void Load(IMainView view)
        {
            if (_batch.Files.Count == 0)
                _batch.Files.AddRange(view.OpenPictureFiles());

            if (_settings.UpgradeRequired)
            {
                _settings.Upgrade();
                _settings.UpgradeRequired = false;
                _settings.Save();
            }

            CurrentPage = new InputViewModel(_settings, this, view, _batch);
        }
    }
}