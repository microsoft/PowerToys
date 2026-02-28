// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using ImageResizer.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ImageResizer.Views
{
    public sealed partial class ResultsPage : Page
    {
        public ResultsViewModel ViewModel { get; set; }

        public ResultsPage()
        {
            InitializeComponent();
        }
    }
}
