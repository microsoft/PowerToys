// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UpdateActivityControl : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(UpdateViewModel),
                typeof(UpdateActivityControl),
                new PropertyMetadata(null));

        public UpdateViewModel ViewModel
        {
            get => (UpdateViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public UpdateActivityControl()
        {
            InitializeComponent();
        }

        public void Open()
        {
            ViewModel?.RequestActivity();
        }
    }
}
