// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ModuleList : UserControl
    {
        private object? _sortButton;

        public ModuleList()
        {
            this.InitializeComponent();
            _sortButton = ModulesCard.TitleContent;
            UpdateHeaderVisibility();
        }

        public bool IsItemClickable
        {
            get => (bool)GetValue(IsItemClickableProperty);
            set => SetValue(IsItemClickableProperty, value);
        }

        public static readonly DependencyProperty IsItemClickableProperty = DependencyProperty.Register(nameof(IsItemClickable), typeof(bool), typeof(ModuleList), new PropertyMetadata(true));

        public object ItemsSource
        {
            get => (object)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(ModuleList), new PropertyMetadata(null));

        public ModuleListSortOption SortOption
        {
            get => (ModuleListSortOption)GetValue(SortOptionProperty);
            set => SetValue(SortOptionProperty, value);
        }

        public static readonly DependencyProperty SortOptionProperty = DependencyProperty.Register(nameof(SortOption), typeof(ModuleListSortOption), typeof(ModuleList), new PropertyMetadata(ModuleListSortOption.Alphabetical));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(ModuleList), new PropertyMetadata(default(string), OnTitleChanged));

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ModuleList)d).UpdateHeaderVisibility();
        }

        private void UpdateHeaderVisibility()
        {
            if (ModulesCard == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(Title))
            {
                ModulesCard.TitleContent = null;
            }
            else
            {
                ModulesCard.TitleContent = _sortButton;
            }
        }

        private void SortAlphabetical_Click(object sender, RoutedEventArgs e)
        {
            SortOption = ModuleListSortOption.Alphabetical;
        }

        private void SortByStatus_Click(object sender, RoutedEventArgs e)
        {
            SortOption = ModuleListSortOption.ByStatus;
        }

        private void OnSettingsCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModuleListItem item)
            {
                item.ClickCommand?.Execute(item.Tag);
            }
        }
    }
}
