// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard
{
    public sealed partial class ShortcutConflictDialogContentControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ConflictsDataProperty =
            DependencyProperty.Register(
                nameof(ConflictsData),
                typeof(AllHotkeyConflictsData),
                typeof(ShortcutConflictDialogContentControl),
                new PropertyMetadata(null, OnConflictsDataChanged));

        public AllHotkeyConflictsData ConflictsData
        {
            get => (AllHotkeyConflictsData)GetValue(ConflictsDataProperty);
            set => SetValue(ConflictsDataProperty, value);
        }

        public List<HotkeyConflictGroupData> ConflictItems { get; private set; } = new List<HotkeyConflictGroupData>();

        private static void OnConflictsDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShortcutConflictDialogContentControl content)
            {
                content.UpdateConflictItems();
            }
        }

        public ShortcutConflictDialogContentControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateConflictItems()
        {
            var items = new List<HotkeyConflictGroupData>();

            if (ConflictsData?.InAppConflicts != null)
            {
                items.AddRange(ConflictsData.InAppConflicts);
            }

            if (ConflictsData?.SystemConflicts != null)
            {
                items.AddRange(ConflictsData.SystemConflicts);
            }

            ConflictItems = items;
            OnPropertyChanged(nameof(ConflictItems));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
