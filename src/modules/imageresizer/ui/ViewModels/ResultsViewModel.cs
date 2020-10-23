// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using System.Windows.Input;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class ResultsViewModel : Observable
    {
        private readonly IMainView _mainView;

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
