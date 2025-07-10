// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutConflictControl : UserControl, INotifyPropertyChanged
    {
        private static readonly ResourceLoader ResourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        public static readonly DependencyProperty AllHotkeyConflictsDataProperty =
            DependencyProperty.Register(
                nameof(AllHotkeyConflictsData),
                typeof(AllHotkeyConflictsData),
                typeof(ShortcutConflictControl),
                new PropertyMetadata(null, OnAllHotkeyConflictsDataChanged));

        public AllHotkeyConflictsData AllHotkeyConflictsData
        {
            get => (AllHotkeyConflictsData)GetValue(AllHotkeyConflictsDataProperty);
            set => SetValue(AllHotkeyConflictsDataProperty, value);
        }

        public int ConflictCount
        {
            get
            {
                if (AllHotkeyConflictsData == null)
                {
                    return 0;
                }

                int count = 0;
                if (AllHotkeyConflictsData.InAppConflicts != null)
                {
                    count += AllHotkeyConflictsData.InAppConflicts.Count;
                }

                if (AllHotkeyConflictsData.SystemConflicts != null)
                {
                    count += AllHotkeyConflictsData.SystemConflicts.Count;
                }

                return count;
            }
        }

        public string ConflictText
        {
            get
            {
                var count = ConflictCount;
                return count switch
                {
                    // Todo: localization support
                    0 => "No conflicts found",
                    1 => "1 conflict found",
                    _ => $"{count} conflicts found",
                };
            }
        }

        public bool HasConflicts => ConflictCount > 0;

        private static void OnAllHotkeyConflictsDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShortcutConflictControl control)
            {
                control.UpdateProperties();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void UpdateProperties()
        {
            OnPropertyChanged(nameof(ConflictCount));
            OnPropertyChanged(nameof(ConflictText));
            OnPropertyChanged(nameof(HasConflicts));

            // Update visibility based on conflict count
            Visibility = HasConflicts ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ShortcutConflictControl()
        {
            InitializeComponent();
            DataContext = this;

            // Initially hide the control if no conflicts
            Visibility = HasConflicts ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void ShortcutConflictBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AllHotkeyConflictsData == null || !HasConflicts)
            {
                return;
            }

            var contentControl = new ShortcutConflictDialogContentControl
            {
                ConflictsData = AllHotkeyConflictsData,
            };

            var conflictDialog = new ContentDialog
            {
                Content = contentControl,
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme,
            };

            // Handle navigation request to close dialog
            contentControl.DialogCloseRequested += (s, args) =>
            {
                conflictDialog.Hide();
            };

            await conflictDialog.ShowAsync();
        }
    }
}
