﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class CmdNotFoundPage : NavigatablePage
    {
        private CmdNotFoundViewModel ViewModel { get; set; }

        public CmdNotFoundPage()
        {
            ViewModel = new CmdNotFoundViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
