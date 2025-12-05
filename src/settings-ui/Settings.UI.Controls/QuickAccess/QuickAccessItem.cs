// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed class QuickAccessItem : DependencyObject
    {
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(QuickAccessItem), new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(QuickAccessItem), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(QuickAccessItem), new PropertyMetadata(string.Empty));

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(QuickAccessItem), new PropertyMetadata(null));

        public object CommandParameter
        {
            get => (object)GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(QuickAccessItem), new PropertyMetadata(null));

        public bool Visible
        {
            get => (bool)GetValue(VisibleProperty);
            set => SetValue(VisibleProperty, value);
        }

        public static readonly DependencyProperty VisibleProperty = DependencyProperty.Register(nameof(Visible), typeof(bool), typeof(QuickAccessItem), new PropertyMetadata(true));

        public object Tag
        {
            get => (object)GetValue(TagProperty);
            set => SetValue(TagProperty, value);
        }

        public static readonly DependencyProperty TagProperty = DependencyProperty.Register(nameof(Tag), typeof(object), typeof(QuickAccessItem), new PropertyMetadata(null));
    }
}
