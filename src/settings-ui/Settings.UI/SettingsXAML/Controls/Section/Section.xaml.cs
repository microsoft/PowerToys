// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Shared section layout: title, canvas, subtitle, then content.
    /// Any part left empty is collapsed.
    /// </summary>
    [ContentProperty(Name = nameof(SectionContent))]
    public sealed partial class Section : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(Section), new PropertyMetadata(string.Empty, OnVisibilityRelevantChanged));

        public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
            nameof(Subtitle), typeof(string), typeof(Section), new PropertyMetadata(string.Empty, OnVisibilityRelevantChanged));

        public static readonly DependencyProperty CanvasContentProperty = DependencyProperty.Register(
            nameof(CanvasContent), typeof(object), typeof(Section), new PropertyMetadata(null, OnVisibilityRelevantChanged));

        public static readonly DependencyProperty SectionContentProperty = DependencyProperty.Register(
            nameof(SectionContent), typeof(object), typeof(Section), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleVisibilityProperty = DependencyProperty.Register(
            nameof(TitleVisibility), typeof(Visibility), typeof(Section), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty SubtitleVisibilityProperty = DependencyProperty.Register(
            nameof(SubtitleVisibility), typeof(Visibility), typeof(Section), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty CanvasVisibilityProperty = DependencyProperty.Register(
            nameof(CanvasVisibility), typeof(Visibility), typeof(Section), new PropertyMetadata(Visibility.Collapsed));

        public Section()
        {
            InitializeComponent();
        }

        [Localizable(true)]
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        [Localizable(true)]
        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public object CanvasContent
        {
            get => GetValue(CanvasContentProperty);
            set => SetValue(CanvasContentProperty, value);
        }

        public object SectionContent
        {
            get => GetValue(SectionContentProperty);
            set => SetValue(SectionContentProperty, value);
        }

        public Visibility TitleVisibility
        {
            get => (Visibility)GetValue(TitleVisibilityProperty);
            private set => SetValue(TitleVisibilityProperty, value);
        }

        public Visibility SubtitleVisibility
        {
            get => (Visibility)GetValue(SubtitleVisibilityProperty);
            private set => SetValue(SubtitleVisibilityProperty, value);
        }

        public Visibility CanvasVisibility
        {
            get => (Visibility)GetValue(CanvasVisibilityProperty);
            private set => SetValue(CanvasVisibilityProperty, value);
        }

        private static void OnVisibilityRelevantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var section = (Section)d;
            section.TitleVisibility = string.IsNullOrWhiteSpace(section.Title) ? Visibility.Collapsed : Visibility.Visible;
            section.SubtitleVisibility = string.IsNullOrWhiteSpace(section.Subtitle) ? Visibility.Collapsed : Visibility.Visible;
            section.CanvasVisibility = section.CanvasContent == null ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
