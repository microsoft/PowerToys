// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ModuleSettingsCard : UserControl
    {
        public ModuleSettingsCard()
        {
            this.InitializeComponent();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ModuleSettingsCard), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ModuleSettingsCard), new PropertyMetadata(null));

        public bool IsNew
        {
            get => (bool)GetValue(IsNewProperty);
            set => SetValue(IsNewProperty, value);
        }

        public static readonly DependencyProperty IsNewProperty = DependencyProperty.Register(nameof(IsNew), typeof(bool), typeof(ModuleSettingsCard), new PropertyMetadata(false));

        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(ModuleSettingsCard), new PropertyMetadata(false));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(ModuleSettingsCard), new PropertyMetadata(false));

        public event RoutedEventHandler? Click;

        private void OnSettingsCardClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }
    }
}
