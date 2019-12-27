using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : ViewModelBase
    {
        readonly ResizeBatch _batch;
        readonly MainViewModel _mainViewModel;
        readonly IMainView _mainView;

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
            settings.CustomSize.PropertyChanged += (sender, e) => settings.SelectedSize = (CustomSize)sender;

            ResizeCommand = new RelayCommand(Resize);
            CancelCommand = new RelayCommand(Cancel);
            ShowAdvancedCommand = new RelayCommand(ShowAdvanced);
        }

        public Settings Settings { get; }

        public ICommand ResizeCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowAdvancedCommand { get; }

        public void Resize()
        {
            Settings.Save();
            _mainViewModel.CurrentPage = new ProgressViewModel(_batch, _mainViewModel, _mainView);
        }

        public void Cancel()
            => _mainView.Close();

        public void ShowAdvanced()
            => _mainView.ShowAdvanced(new AdvancedViewModel(Settings));
    }
}
