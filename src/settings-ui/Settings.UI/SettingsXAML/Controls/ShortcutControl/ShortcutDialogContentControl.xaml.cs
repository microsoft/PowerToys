// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutDialogContentControl : UserControl
    {
        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register("Keys", typeof(List<object>), typeof(ShortcutDialogContentControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register("IsError", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty IsWarningAltGrProperty = DependencyProperty.Register("IsWarningAltGr", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty HasConflictProperty = DependencyProperty.Register("HasConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false));
        public static readonly DependencyProperty ConflictMessageProperty = DependencyProperty.Register("ConflictMessage", typeof(string), typeof(ShortcutDialogContentControl), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty IgnoreConflictProperty = DependencyProperty.Register("IgnoreConflict", typeof(bool), typeof(ShortcutDialogContentControl), new PropertyMetadata(false, OnIgnoreConflictChanged));

        public event EventHandler<bool> IgnoreConflictChanged;

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

        public ShortcutDialogContentControl()
        {
            this.InitializeComponent();
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

        private static void OnIgnoreConflictChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ShortcutDialogContentControl;
            if (control == null)
            {
                return;
            }

            control.IgnoreConflictChanged?.Invoke(control, (bool)e.NewValue);
        }
    }
}
