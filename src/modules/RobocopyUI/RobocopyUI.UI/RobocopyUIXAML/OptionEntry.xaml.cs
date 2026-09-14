// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

namespace RobocopyUI
{
    public sealed partial class OptionEntry : UserControl
    {
        public string OptionName
        {
            get { return (string)GetValue(OptionNameProperty); }
            set { SetValue(OptionNameProperty, value); }
        }

        public string OptionDescription
        {
            get { return (string)GetValue(OptionDescriptionProperty); }
            set { SetValue(OptionDescriptionProperty, value); }
        }

        public object OptionContent
        {
            get { return (object)GetValue(OptionContentProperty); }
            set { SetValue(OptionContentProperty, value); }
        }

        public static readonly Microsoft.UI.Xaml.DependencyProperty OptionContentProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register("OptionContent", typeof(object), typeof(OptionEntry), new Microsoft.UI.Xaml.PropertyMetadata(null));

        public static readonly Microsoft.UI.Xaml.DependencyProperty OptionDescriptionProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register("OptionDescription", typeof(string), typeof(OptionEntry), new Microsoft.UI.Xaml.PropertyMetadata(string.Empty));

        public static readonly Microsoft.UI.Xaml.DependencyProperty OptionNameProperty =
            Microsoft.UI.Xaml.DependencyProperty.Register("OptionName", typeof(string), typeof(OptionEntry), new Microsoft.UI.Xaml.PropertyMetadata(string.Empty));

        public OptionEntry()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
}
