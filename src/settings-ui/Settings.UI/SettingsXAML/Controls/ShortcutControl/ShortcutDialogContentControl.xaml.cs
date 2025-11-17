// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutDialogContentControl : UserControl
    {
        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register("Keys", typeof(List<object>), typeof(ShortcutDialogContentControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register("IsError", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty IsWarningAltGrProperty = DependencyProperty.Register("IsWarningAltGr", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty HasConflictProperty = DependencyProperty.Register("HasConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false, OnConflictPropertyChanged));
        public static readonly DependencyProperty ConflictMessageProperty = DependencyProperty.Register("ConflictMessage", typeof(string), typeof(ShortcutDialogContentControl), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty IgnoreConflictProperty = DependencyProperty.Register("IgnoreConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false, OnIgnoreConflictChanged));

        public static readonly DependencyProperty ShouldShowConflictProperty = DependencyProperty.Register("ShouldShowConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty ShouldShowPotentialConflictProperty = DependencyProperty.Register("ShouldShowPotentialConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));

        public event EventHandler<bool> IgnoreConflictChanged;

        public event RoutedEventHandler LearnMoreClick;

        public bool IgnoreConflict
        {
            get => (bool)GetValue(IgnoreConflictProperty);
            set => SetValue(IgnoreConflictProperty, value);
        }

        public bool HasConflict
        {
            get => (bool)GetValue(HasConflictProperty);
            set => SetValue(HasConflictProperty, value);
        }

        public string ConflictMessage
        {
            get => (string)GetValue(ConflictMessageProperty);
            set => SetValue(ConflictMessageProperty, value);
        }

        public bool ShouldShowConflict
        {
            get => (bool)GetValue(ShouldShowConflictProperty);
            private set => SetValue(ShouldShowConflictProperty, value);
        }

        public bool ShouldShowPotentialConflict
        {
            get => (bool)GetValue(ShouldShowPotentialConflictProperty);
            private set => SetValue(ShouldShowPotentialConflictProperty, value);
        }

        public ShortcutDialogContentControl()
        {
            this.InitializeComponent();
            UpdateShouldShowConflict();
        }

        public List<object> Keys
        {
            get { return (List<object>)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public bool IsWarningAltGr
        {
            get => (bool)GetValue(IsWarningAltGrProperty);
            set => SetValue(IsWarningAltGrProperty, value);
        }

        public event RoutedEventHandler ResetClick;

        public event RoutedEventHandler ClearClick;

        private static void OnIgnoreConflictChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutDialogContentControl;
            if (control == null)
            {
                return;
            }

            control.UpdateShouldShowConflict();

            control.IgnoreConflictChanged?.Invoke(control, (bool)e.NewValue);
        }

        private static void OnConflictPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutDialogContentControl;
            if (control == null)
            {
                return;
            }

            control.UpdateShouldShowConflict();
        }

        private void UpdateShouldShowConflict()
        {
            ShouldShowConflict = !IgnoreConflict && HasConflict;
            ShouldShowPotentialConflict = IgnoreConflict && HasConflict;
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetClick?.Invoke(this, new RoutedEventArgs());
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearClick?.Invoke(this, new RoutedEventArgs());
        }

        private void LearnMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LearnMoreClick?.Invoke(this, new RoutedEventArgs());
        }
    }
}
