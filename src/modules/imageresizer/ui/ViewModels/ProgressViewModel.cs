// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Views;
using Microsoft.UI.Dispatching;

namespace ImageResizer.ViewModels
{
    public partial class ProgressViewModel : ObservableObject, IDisposable
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ResizeBatch _batch;
        private readonly IMainView _mainView;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _disposedValue;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TimeRemainingDisplay))]
        private TimeSpan _timeRemaining;

        private static CompositeFormat _progressTimeRemainingFormat;

        private static CompositeFormat ProgressTimeRemainingFormat
        {
            get
            {
                if (_progressTimeRemainingFormat == null)
                {
                    var formatString = ResourceLoaderInstance.ResourceLoader.GetString("Progress_TimeRemaining");
                    _progressTimeRemainingFormat = CompositeFormat.Parse(formatString);
                }

                return _progressTimeRemainingFormat;
            }
        }

        public string TimeRemainingDisplay
        {
            get
            {
                if (TimeRemaining == TimeSpan.MaxValue || TimeRemaining.TotalSeconds < 1)
                {
                    return string.Empty;
                }

                return string.Format(CultureInfo.CurrentCulture, ProgressTimeRemainingFormat, TimeRemaining);
            }
        }

        public ProgressViewModel(
            ResizeBatch batch,
            MainViewModel mainViewModel,
            IMainView mainView)
        {
            _batch = batch;
            _mainViewModel = mainViewModel;
            _mainView = mainView;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        [RelayCommand]
        public void Start()
        {
            _ = Task.Run(() => StartExecutingWorkAsync());
        }

        private async Task StartExecutingWorkAsync()
        {
            _stopwatch.Restart();
            var errors = await _batch.ProcessAsync(
                (completed, total) =>
                {
                    var progress = completed / total;
                    var timeRemaining = _stopwatch.Elapsed.Multiply((total - completed) / completed);

                    // Dispatch UI updates to the UI thread
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        Progress = progress;
                        _mainViewModel.Progress = progress;
                        TimeRemaining = timeRemaining;
                    });
                },
                _cancellationTokenSource.Token);

            // Dispatch final UI updates to the UI thread
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (errors.Any())
                {
                    _mainViewModel.Progress = 0;
                    _mainViewModel.CurrentPage = new ResultsViewModel(_mainView, errors);
                }
                else
                {
                    _mainView.Close();
                }
            });
        }

        [RelayCommand]
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _mainView.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
