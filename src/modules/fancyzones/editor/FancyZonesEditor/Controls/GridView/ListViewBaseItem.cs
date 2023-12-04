// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernWpf.Controls.Primitives;

namespace FancyZonesEditor.Controls
{
    public class ListViewBaseItem : ListBoxItem
    {
        protected ListViewBaseItem()
        {
        }

        public static readonly DependencyProperty UseSystemFocusVisualsProperty =
            FocusVisualHelper.UseSystemFocusVisualsProperty.AddOwner(typeof(ListViewBaseItem));

        public bool UseSystemFocusVisuals
        {
            get => (bool)GetValue(UseSystemFocusVisualsProperty);
            set => SetValue(UseSystemFocusVisualsProperty, value);
        }

        public static readonly DependencyProperty FocusVisualMarginProperty =
            FocusVisualHelper.FocusVisualMarginProperty.AddOwner(typeof(ListViewBaseItem));

        public Thickness FocusVisualMargin
        {
            get => (Thickness)GetValue(FocusVisualMarginProperty);
            set => SetValue(FocusVisualMarginProperty, value);
        }

        public static readonly DependencyProperty FocusVisualPrimaryBrushProperty =
            FocusVisualHelper.FocusVisualPrimaryBrushProperty.AddOwner(typeof(ListViewBaseItem));

        public Thickness FocusVisualPrimaryBrush
        {
            get => (Thickness)GetValue(FocusVisualPrimaryBrushProperty);
            set => SetValue(FocusVisualPrimaryBrushProperty, value);
        }

        public static readonly DependencyProperty FocusVisualPrimaryThicknessProperty =
            FocusVisualHelper.FocusVisualPrimaryThicknessProperty.AddOwner(typeof(ListViewBaseItem));

        public Thickness FocusVisualPrimaryThickness
        {
            get => (Thickness)GetValue(FocusVisualPrimaryThicknessProperty);
            set => SetValue(FocusVisualPrimaryThicknessProperty, value);
        }

        public static readonly DependencyProperty FocusVisualSecondaryBrushProperty =
            FocusVisualHelper.FocusVisualSecondaryBrushProperty.AddOwner(typeof(ListViewBaseItem));

        public Thickness FocusVisualSecondaryBrush
        {
            get => (Thickness)GetValue(FocusVisualSecondaryBrushProperty);
            set => SetValue(FocusVisualSecondaryBrushProperty, value);
        }

        public static readonly DependencyProperty FocusVisualSecondaryThicknessProperty =
            FocusVisualHelper.FocusVisualSecondaryThicknessProperty.AddOwner(typeof(ListViewBaseItem));

        public Thickness FocusVisualSecondaryThickness
        {
            get => (Thickness)GetValue(FocusVisualSecondaryThicknessProperty);
            set => SetValue(FocusVisualSecondaryThicknessProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            ControlHelper.CornerRadiusProperty.AddOwner(typeof(ListViewBaseItem));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            UpdateMultiSelectStates(ParentListViewBase, false);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!e.Handled)
            {
                isPressed = true;
            }

            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!e.Handled)
            {
                HandleMouseUp(e);
                isPressed = false;
            }

            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (!e.Handled)
            {
                isPressed = false;
            }

            base.OnMouseLeave(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Enter)
            {
                OnClick();
                e.Handled = true;
            }
        }

        internal void SubscribeToMultiSelectEnabledChanged(ListViewBase parent)
        {
            parent.MultiSelectEnabledChanged += OnMultiSelectEnabledChanged;
            UpdateMultiSelectStates(parent);
        }

        internal void UnsubscribeFromMultiSelectEnabledChanged(ListViewBase parent)
        {
            parent.MultiSelectEnabledChanged -= OnMultiSelectEnabledChanged;
            UpdateMultiSelectStates(parent);
        }

        private void OnMultiSelectEnabledChanged(object sender, EventArgs e)
        {
            UpdateMultiSelectStates((ListViewBase)sender);
        }

        private void UpdateMultiSelectStates(ListViewBase parent, bool useTransitions = true)
        {
            if (parent != null)
            {
                bool enabled = parent.MultiSelectEnabled && parent.IsMultiSelectCheckBoxEnabled;
                VisualStateManager.GoToState(this, enabled ? "MultiSelectEnabled" : "MultiSelectDisabled", useTransitions);
            }
        }

        private void HandleMouseUp(MouseButtonEventArgs e)
        {
            if (isPressed)
            {
#pragma warning disable SA1129 // Do not use default value type constructor
                Rect r = new Rect(new Point(), RenderSize);
#pragma warning restore SA1129 // Do not use default value type constructor

                if (r.Contains(e.GetPosition(this)))
                {
                    OnClick();
                }
            }
        }

        private void OnClick()
        {
            ParentListViewBase?.NotifyListItemClicked(this);
        }

        private ListViewBase ParentListViewBase => ItemsControl.ItemsControlFromItemContainer(this) as ListViewBase;

        private bool isPressed;
    }
}
