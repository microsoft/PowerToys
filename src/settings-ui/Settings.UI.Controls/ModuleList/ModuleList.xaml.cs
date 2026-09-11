// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ModuleList : UserControl
    {
        public ModuleList()
        {
            this.InitializeComponent();
        }

        public Thickness DividerThickness
        {
            get => (Thickness)GetValue(DividerThicknessProperty);
            set => SetValue(DividerThicknessProperty, value);
        }

        public static readonly DependencyProperty DividerThicknessProperty = DependencyProperty.Register(nameof(DividerThickness), typeof(Thickness), typeof(ModuleList), new PropertyMetadata(new Thickness(0, 1, 0, 0)));

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

        public Style? ItemContainerStyle
        {
            get => (Style?)GetValue(ItemContainerStyleProperty);
            set => SetValue(ItemContainerStyleProperty, value);
        }

        public static readonly DependencyProperty ItemContainerStyleProperty = DependencyProperty.Register(
            nameof(ItemContainerStyle),
            typeof(Style),
            typeof(ModuleList),
            new PropertyMetadata(null, OnItemContainerStyleChanged));

        public ModuleListSortOption SortOption
        {
            get => (ModuleListSortOption)GetValue(SortOptionProperty);
            set => SetValue(SortOptionProperty, value);
        }

        public static readonly DependencyProperty SortOptionProperty = DependencyProperty.Register(nameof(SortOption), typeof(ModuleListSortOption), typeof(ModuleList), new PropertyMetadata(ModuleListSortOption.Alphabetical));

        private static void OnItemContainerStyleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var control = (ModuleList)sender;
            if (control.DashboardView?.ItemsSourceView is not { } items)
            {
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (control.DashboardView.TryGetElement(index) is SettingsCard card)
                {
                    control.ApplyItemContainerStyle(card);
                }
            }
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is SettingsCard card)
            {
                // Apply before the first measure so the default template is not built and then replaced.
                ApplyItemContainerStyle(card);
            }
        }

        private void ApplyItemContainerStyle(SettingsCard card)
        {
            if (ItemContainerStyle is { } style)
            {
                card.Style = style;
            }
            else
            {
                card.ClearValue(StyleProperty);
            }
        }

        private void OnSettingsCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is ModuleListItem item)
            {
                item.ClickCommand?.Execute(item.Tag);
            }
        }
    }
}
