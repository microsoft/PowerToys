// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class Card : UserControl
    {
        public static readonly DependencyProperty TitleContentProperty = DependencyProperty.Register(nameof(TitleContent), typeof(object), typeof(Card), new PropertyMetadata(defaultValue: null, OnVisualPropertyChanged));

        public object TitleContent
        {
            get => (object)GetValue(TitleContentProperty);
            set => SetValue(TitleContentProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(defaultValue: null, OnVisualPropertyChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static new readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Card), new PropertyMetadata(defaultValue: null));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1061:Do not hide base class methods", Justification = "We need to hide the base class method")]
        public new object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty DividerVisibilityProperty = DependencyProperty.Register(nameof(DividerVisibility), typeof(Visibility), typeof(Card), new PropertyMetadata(defaultValue: null));

        public Visibility DividerVisibility
        {
            get => (Visibility)GetValue(DividerVisibilityProperty);
            set => SetValue(DividerVisibilityProperty, value);
        }

        public Card()
        {
            InitializeComponent();
            SetVisualStates();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Card card)
            {
                card.SetVisualStates();
            }
        }

        private void SetVisualStates()
        {
            if (string.IsNullOrEmpty(Title) && TitleContent == null)
            {
                VisualStateManager.GoToState(this, "TitleGridCollapsed", true);
                DividerVisibility = Visibility.Collapsed;
            }
            else
            {
                VisualStateManager.GoToState(this, "TitleGridVisible", true);
                DividerVisibility = Visibility.Visible;
            }
        }
    }
}
