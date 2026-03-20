#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Collections.Generic;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class ResultsViewModel : ObservableObject
    {
        private readonly IMainView _mainView;

        public ResultsViewModel(IMainView mainView, IEnumerable<ResizeError> errors)
        {
            _mainView = mainView;
            Errors = errors;
            CloseCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Close);
        }

        public IEnumerable<ResizeError> Errors { get; }

        public ICommand CloseCommand { get; }

        public void Close() => _mainView.Close();
    }
}
