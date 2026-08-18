// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UpdateStatusControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(UpdateViewModel),
                typeof(UpdateStatusControl),
                new PropertyMetadata(null));

        public string CloseButtonText { get; } = ResourceLoaderInstance.ResourceLoader.GetString("ColorPicker_Close/Content");

        public UpdateViewModel ViewModel
        {
            get => (UpdateViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public UpdateStatusControl()
        {
            InitializeComponent();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.DismissActivity();
        }

        private void SeeWhatsNewButton_Click(object sender, RoutedEventArgs e)
        {
            ((App)App.Current)!.OpenScoobe();
        }
    }
}
