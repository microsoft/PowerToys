// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class ProgressViewModel : Observable, IDisposable
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ResizeBatch _batch;
        private readonly IMainView _mainView;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private double _progress;
        private TimeSpan _timeRemaining;
        private bool disposedValue;

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
            set => Set(ref _progress, value);
        }

        public TimeSpan TimeRemaining
        {
            get => _timeRemaining;
            set => Set(ref _timeRemaining, value);
        }

        public ICommand StartCommand { get; }

        public ICommand StopCommand { get; }

        public void Start()
        {
            _ = Task.Factory.StartNew(StartExecutingWork, _cancellationTokenSource.Token, TaskCreationOptions.None, TaskScheduler.Current);
        }

        private void StartExecutingWork()
        {
            _stopwatch.Restart();
            var errors = _batch.Process(
                (completed, total) =>
                {
                    var progress = completed / total;
                    Progress = progress;
                    _mainViewModel.Progress = progress;

                    TimeRemaining = _stopwatch.Elapsed.Multiply((total - completed) / completed);
                },
                _cancellationTokenSource.Token);

            if (errors.Any())
            {
                _mainViewModel.Progress = 0;
                _mainViewModel.CurrentPage = new ResultsViewModel(_mainView, errors);
            }
            else
            {
                _mainView.Close();
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _mainView.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
