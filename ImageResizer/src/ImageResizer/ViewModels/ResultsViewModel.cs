using System.Collections.Generic;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class ResultsViewModel : ViewModelBase
    {
        readonly IMainView _mainView;

        public ResultsViewModel(IMainView mainView, IEnumerable<ResizeError> errors)
        {
            _mainView = mainView;
            Errors = errors;
            CloseCommand = new RelayCommand(Close);
        }

        public IEnumerable<ResizeError> Errors { get; }
        public ICommand CloseCommand { get; }

        public void Close() => _mainView.Close();
    }
}
