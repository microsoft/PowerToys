// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutConflictControl : UserControl, INotifyPropertyChanged
    {
        private static readonly ResourceLoader ResourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        private static bool _telemetryEventSent;

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
                    foreach (var inAppConflict in AllHotkeyConflictsData.InAppConflicts)
                    {
                        if (!inAppConflict.ConflictIgnored)
                        {
                            count++;
                        }
                    }
                }

                if (AllHotkeyConflictsData.SystemConflicts != null)
                {
                    foreach (var systemConflict in AllHotkeyConflictsData.SystemConflicts)
                    {
                        if (!systemConflict.ConflictIgnored)
                        {
                            count++;
                        }
                    }
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
                    0 => ResourceLoader.GetString("ShortcutConflictControl_NoConflictsFound"),
                    1 => ResourceLoader.GetString("ShortcutConflictControl_SingleConflictFound"),
                    _ => string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        ResourceLoader.GetString("ShortcutConflictControl_MultipleConflictsFound"),
                        count),
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
            if (HasConflicts)
            {
                VisualStateManager.GoToState(this, "ConflictState", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "NoConflictState", true);
            }

            if (!_telemetryEventSent && HasConflicts)
            {
                // Log telemetry event when conflicts are detected
                PowerToysTelemetry.Log.WriteEvent(new ShortcutConflictDetectedEvent()
                {
                    ConflictCount = ConflictCount,
                });

                _telemetryEventSent = true;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ShortcutConflictControl()
        {
            InitializeComponent();
            DataContext = this;

            UpdateProperties();
        }

        private void ShortcutConflictBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AllHotkeyConflictsData == null)
            {
                return;
            }

            // Log telemetry event when user clicks the shortcut conflict button
            PowerToysTelemetry.Log.WriteEvent(new ShortcutConflictControlClickedEvent()
            {
                ConflictCount = this.ConflictCount,
            });

            ((App)App.Current)!.OpenShortcutConflictWindow();
        }
    }
}
