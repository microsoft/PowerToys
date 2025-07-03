// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutConflictControl : UserControl
    {
        private AllHotkeyConflictsData _currentConflicts;

        public ShortcutConflictControl()
        {
            InitializeComponent();
            RegisterForConflictUpdates();
            GetShortcutConflicts();
        }

        private void RegisterForConflictUpdates()
        {
            // Subscribe to conflict updates from the global manager
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
            }
        }

        private void GetShortcutConflicts()
        {
            // Request all hotkey conflicts via IPC
            GlobalHotkeyConflictManager.Instance?.RequestAllConflicts();
        }

        private void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            _currentConflicts = e.Conflicts;

            // Update UI on the main thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateConflictDisplay();
            });
        }

        private void UpdateConflictDisplay()
        {
            if (_currentConflicts == null)
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }

            // Check if there are any conflicts
            bool hasConflicts = _currentConflicts.InAppConflicts.Count != 0 || _currentConflicts.SystemConflicts.Count != 0;

            if (!hasConflicts)
            {
                this.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.Visibility = Visibility.Visible;

                // TODO: Update UI elements to show conflict count or details
                // For example, update button text with conflict count
                // ShortcutConflictBtn.Content = $"View {GetTotalConflictCount()} conflicts";
            }
        }

        private int GetTotalConflictCount()
        {
            if (_currentConflicts == null)
            {
                return 0;
            }

            return _currentConflicts.InAppConflicts.Count + _currentConflicts.SystemConflicts.Count;
        }

        private void ShortcutConflictBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Handle the button click event to show the shortcut conflicts window.
            // You can now use _currentConflicts to display detailed conflict information
        }

        // Clean up event subscription when control is unloaded
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
            }
        }
    }
}
