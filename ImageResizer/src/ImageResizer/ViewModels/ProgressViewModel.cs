using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class ProgressViewModel : ViewModelBase
    {
        readonly MainViewModel _mainViewModel;
        readonly ResizeBatch _batch;
        readonly IMainView _mainView;
        readonly Stopwatch _stopwatch = new Stopwatch();
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        double _progress;
        TimeSpan _timeRemaining;

        public ProgressViewModel(
            ResizeBatch batch,
            MainViewModel mainViewModel,
            IMainView mainView)
        {
            _batch = batch;
            _mainViewModel = mainViewModel;
            _mainView = mainView;

            StartCommand = new RelayCommand(Start);
            StopCommand = new RelayCommand(Stop);
        }

        public double Progress
        {
            get => _progress;
            set => Set(nameof(Progress), ref _progress, value);
        }

        public TimeSpan TimeRemaining
        {
            get => _timeRemaining;
            set => Set(nameof(TimeRemaining), ref _timeRemaining, value);
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public void Start()
            => Task.Factory.StartNew(
                () =>
                {
                    _stopwatch.Restart();
                    var errors = _batch.Process(
                        _cancellationTokenSource.Token,
                        (completed, total) =>
                        {
                            var progress = completed / total;
                            Progress = progress;
                            _mainViewModel.Progress = progress;

                            TimeRemaining = _stopwatch.Elapsed.Multiply((total - completed) / completed);
                        });

                    if (errors.Any())
                    {
                        _mainViewModel.Progress = 0;
                        _mainViewModel.CurrentPage = new ResultsViewModel(_mainView, errors);
                    }
                    else
                    {
                        _mainView.Close();
                    }
                },
                _cancellationTokenSource.Token);

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _mainView.Close();
        }
    }
}
