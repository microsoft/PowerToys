// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    public class IsEnabledTextBlock : Control
    {
        public IsEnabledTextBlock()
        {
            this.Style = (Style)App.Current.Resources["DefaultIsEnabledTextBlockStyle"];
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= IsEnabledTextBlock_IsEnabledChanged;
            SetEnabledState();
            IsEnabledChanged += IsEnabledTextBlock_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
           "Text",
           typeof(string),
           typeof(IsEnabledTextBlock),
           null);

        [Localizable(true)]
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private void IsEnabledTextBlock_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }
}
