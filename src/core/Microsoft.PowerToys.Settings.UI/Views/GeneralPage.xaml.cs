// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// General Settings Page.
    /// </summary>
    public sealed partial class GeneralPage : Page
    {
        /// <summary>
        /// Gets or sets view model.
        /// </summary>
        public GeneralViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralPage"/> class.
        /// General Settings page constructor.
        /// </summary>
        public GeneralPage()
        {
            this.InitializeComponent();

            this.ViewModel = new GeneralViewModel();
            this.GeneralSettingsView.DataContext = this.ViewModel;
        }
    }
}
