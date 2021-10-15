// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Windows.UI.Accessibility;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public class CheckBoxSubTextControl : CheckBox
    {
        private CheckBoxSubTextControl _checkBoxSubTextControl;

        public CheckBoxSubTextControl()
        {
            this.DefaultStyleKey = typeof(CheckBoxSubTextControl);
            _checkBoxSubTextControl = (CheckBoxSubTextControl)this;
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(CheckBoxSubTextControl),
            new PropertyMetadata(default(string), OnHeaderChanged));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(object),
            typeof(CheckBoxSubTextControl),
            new PropertyMetadata(null, OnDescriptionChanged));

        [Localizable(true)]
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        [Localizable(true)]
        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CheckBoxSubTextControl)d).Update();
        }

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CheckBoxSubTextControl)d).Update();
        }

        private void Update()
        {
            if (_checkBoxSubTextControl == null)
            {
                return;
            }

            StackPanel panel = new StackPanel() { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock() { Margin = new Thickness(0, 10, 0, 0), Text = Header });
            panel.Children.Add(new TextBlockControl() { FontSize = 10, Text = Description });
            _checkBoxSubTextControl.Content = _checkBoxSubTextControl;
        }
    }
}
