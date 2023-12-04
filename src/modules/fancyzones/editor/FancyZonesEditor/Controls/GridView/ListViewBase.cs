// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using ModernWpf.Controls.Primitives;

namespace FancyZonesEditor.Controls
{
    public class ListViewBase : ListBox
    {
        static ListViewBase()
        {
            SelectionModeProperty.OverrideMetadata(typeof(ListViewBase), new FrameworkPropertyMetadata(OnSelectionModePropertyChanged));
        }

        protected ListViewBase()
        {
            UpdateMultiSelectEnabled();
        }

        public static readonly DependencyProperty IsItemClickEnabledProperty =
            DependencyProperty.Register(
                nameof(IsItemClickEnabled),
                typeof(bool),
                typeof(ListViewBase),
                new PropertyMetadata(false));

        public bool IsItemClickEnabled
        {
            get => (bool)GetValue(IsItemClickEnabledProperty);
            set => SetValue(IsItemClickEnabledProperty, value);
        }

        public static readonly DependencyProperty IsSelectionEnabledProperty =
            DependencyProperty.Register(
                nameof(IsSelectionEnabled),
                typeof(bool),
                typeof(ListViewBase),
                new PropertyMetadata(true, OnIsSelectionEnabledChanged));

        public bool IsSelectionEnabled
        {
            get => (bool)GetValue(IsSelectionEnabledProperty);
            set => SetValue(IsSelectionEnabledProperty, value);
        }

        private static void OnIsSelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lvb = (ListViewBase)d;
            lvb.UpdateMultiSelectEnabled();
            if (!(bool)e.NewValue)
            {
                if (lvb.SelectedItems.Count > 0)
                {
                    lvb.UnselectAll();
                }
            }
        }

        public static readonly DependencyProperty IsMultiSelectCheckBoxEnabledProperty =
            DependencyProperty.Register(
                nameof(IsMultiSelectCheckBoxEnabled),
                typeof(bool),
                typeof(ListViewBase),
                new PropertyMetadata(true, OnIsMultiSelectCheckBoxEnabledChanged));

        public bool IsMultiSelectCheckBoxEnabled
        {
            get => (bool)GetValue(IsMultiSelectCheckBoxEnabledProperty);
            set => SetValue(IsMultiSelectCheckBoxEnabledProperty, value);
        }

        private static void OnIsMultiSelectCheckBoxEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ListViewBase)d).UpdateMultiSelectEnabled();
        }

        public static readonly DependencyProperty UseSystemFocusVisualsProperty =
            FocusVisualHelper.UseSystemFocusVisualsProperty.AddOwner(typeof(ListViewBase));

        public bool UseSystemFocusVisuals
        {
            get => (bool)GetValue(UseSystemFocusVisualsProperty);
            set => SetValue(UseSystemFocusVisualsProperty, value);
        }

        public static readonly DependencyProperty FocusVisualMarginProperty =
            FocusVisualHelper.FocusVisualMarginProperty.AddOwner(typeof(ListViewBase));

        public Thickness FocusVisualMargin
        {
            get => (Thickness)GetValue(FocusVisualMarginProperty);
            set => SetValue(FocusVisualMarginProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            ControlHelper.CornerRadiusProperty.AddOwner(typeof(ListViewBase));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        internal bool MultiSelectEnabled
        {
            get => multiSelectEnabled;
            set
            {
                if (multiSelectEnabled != value)
                {
                    multiSelectEnabled = value;
                    MultiSelectEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event ItemClickEventHandler ItemClick;

        internal event EventHandler MultiSelectEnabledChanged;

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is ListViewBaseItem lvi)
            {
                lvi.SubscribeToMultiSelectEnabledChanged(this);
            }
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);

            if (element is ListViewBaseItem lvi)
            {
                lvi.UnsubscribeFromMultiSelectEnabledChanged(this);
            }
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (IsSelectionEnabled)
            {
                base.OnSelectionChanged(e);
            }
            else
            {
                if (SelectedItems.Count > 0)
                {
                    UnselectAll();
                }
            }
        }

        internal void NotifyListItemClicked(ListViewBaseItem item)
        {
            if (IsItemClickEnabled)
            {
                var clickedItem = ItemContainerGenerator.ItemFromContainer(item);
                ItemClick?.Invoke(this, new ItemClickEventArgs { ClickedItem = clickedItem });
            }
        }

        private static void OnSelectionModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ListViewBase)d).UpdateMultiSelectEnabled();
        }

        private void UpdateMultiSelectEnabled()
        {
            MultiSelectEnabled = IsSelectionEnabled &&
                                 SelectionMode == SelectionMode.Multiple &&
                                 IsMultiSelectCheckBoxEnabled;
        }

        private bool multiSelectEnabled;
    }
}
