// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageResizer.Models;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public partial class ResultsViewModel : ObservableObject
    {
        private readonly IMainView _mainView;

        public ResultsViewModel(IMainView mainView, IEnumerable<ResizeError> errors)
        {
            _mainView = mainView;
            Errors = errors;
        }

        public IEnumerable<ResizeError> Errors { get; }

        [RelayCommand]
        public void Close() => _mainView.Close();
    }
}
