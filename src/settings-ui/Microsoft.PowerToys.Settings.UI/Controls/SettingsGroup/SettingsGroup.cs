// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Represents a control that can contain multiple settings (or other) controls
    /// </summary>
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    public partial class SettingsGroup : ItemsControl
    {
        public SettingsGroup()
        {
            DefaultStyleKey = typeof(SettingsGroup);
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(SettingsGroup),
            new PropertyMetadata(default(string)));

        [Localizable(true)]
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= SettingsGroup_IsEnabledChanged;
            SetEnabledState();
            IsEnabledChanged += SettingsGroup_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private void SettingsGroup_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }
}
