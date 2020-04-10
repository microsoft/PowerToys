// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class FancyZonesPage : Page
    {
        public FancyZonesViewModel ViewModel { get; } = new FancyZonesViewModel();

        public FancyZonesPage()
        {
            InitializeComponent();
        }
    }
}
