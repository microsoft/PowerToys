// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeOverview : Page, INotifyPropertyChanged
    {
        public OobePowerToysModule ViewModel { get; set; }

        private bool _enableDataDiagnostics;
        private AllHotkeyConflictsData _allHotkeyConflictsData = new AllHotkeyConflictsData();
        private Windows.ApplicationModel.Resources.ResourceLoader resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

        private int _conflictCount;

        public bool EnableDataDiagnostics
        {
            get
            {
                return _enableDataDiagnostics;
            }

            set
            {
                if (_enableDataDiagnostics != value)
                {
                    _enableDataDiagnostics = value;

                    DataDiagnosticsSettings.SetEnabledValue(_enableDataDiagnostics);

                    this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        ShellPage.ShellHandler?.SignalGeneralDataUpdate();
                    });
                }
            }
        }

        public AllHotkeyConflictsData AllHotkeyConflictsData
        {
            get => _allHotkeyConflictsData;
            set
            {
                if (_allHotkeyConflictsData != value)
                {
                    _allHotkeyConflictsData = value;

                    UpdateConflictCount();

                    OnPropertyChanged(nameof(AllHotkeyConflictsData));
                    OnPropertyChanged(nameof(ConflictCount));
                    OnPropertyChanged(nameof(ConflictText));
                    OnPropertyChanged(nameof(ConflictDescription));
                    OnPropertyChanged(nameof(HasConflicts));
                    OnPropertyChanged(nameof(IconGlyph));
                    OnPropertyChanged(nameof(IconForeground));
                }
            }
        }

        public int ConflictCount => _conflictCount;

        private void UpdateConflictCount()
        {
            int count = 0;
            if (AllHotkeyConflictsData == null)
            {
                _conflictCount = count;
            }

            if (AllHotkeyConflictsData.InAppConflicts != null)
            {
                foreach (var inAppConflict in AllHotkeyConflictsData.InAppConflicts)
                {
                    var hotkey = inAppConflict.Hotkey;
                    var hotkeySettings = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    if (!HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySettings))
                    {
                        count++;
                    }
                }
            }

            if (AllHotkeyConflictsData.SystemConflicts != null)
            {
                foreach (var systemConflict in AllHotkeyConflictsData.SystemConflicts)
                {
                    var hotkey = systemConflict.Hotkey;
                    var hotkeySettings = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Key);
                    if (!HotkeyConflictIgnoreHelper.IsIgnoringConflicts(hotkeySettings))
                    {
                        count++;
                    }
                }
            }

            _conflictCount = count;
        }

        public string ConflictText
        {
            get
            {
                var count = ConflictCount;
                if (count == 0)
                {
                    // Return no-conflict message
                    try
                    {
                        return resourceLoader.GetString("ShortcutConflictControl_NoConflictsFound");
                    }
                    catch
                    {
                        return "No conflicts found";
                    }
                }
                else if (count == 1)
                {
                    // Try to get localized string
                    try
                    {
                        return resourceLoader.GetString("ShortcutConflictControl_SingleConflictFound");
                    }
                    catch
                    {
                        return "1 shortcut conflict";
                    }
                }
                else
                {
                    // Try to get localized string
                    try
                    {
                        var template = resourceLoader.GetString("ShortcutConflictControl_MultipleConflictsFound");
                        return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, count);
                    }
                    catch
                    {
                        return $"{count} shortcut conflicts";
                    }
                }
            }
        }

        public string ConflictDescription
        {
            get
            {
                var count = ConflictCount;
                if (count == 0)
                {
                    // Return no-conflict description
                    try
                    {
                        return resourceLoader.GetString("ShortcutConflictWindow_NoConflictsDescription");
                    }
                    catch
                    {
                        return "All shortcuts function correctly";
                    }
                }
                else
                {
                    // Return conflict description
                    try
                    {
                        return resourceLoader.GetString("Oobe_Overview_Hotkey_Conflict_Card_Description");
                    }
                    catch
                    {
                        return "Shortcuts configured by PowerToys are conflicting";
                    }
                }
            }
        }

        public bool HasConflicts => ConflictCount > 0;

        public string IconGlyph => HasConflicts ? "\uE814" : "\uE73E";

        public SolidColorBrush IconForeground
        {
            get
            {
                if (HasConflicts)
                {
                    // Red color for conflicts
                    return (SolidColorBrush)App.Current.Resources["SystemFillColorCriticalBrush"];
                }
                else
                {
                    // Green color for no conflicts
                    return (SolidColorBrush)App.Current.Resources["SystemFillColorSuccessBrush"];
                }
            }
        }

        public bool ShowDataDiagnosticsSetting => GetIsDataDiagnosticsInfoBarEnabled();

        public event PropertyChangedEventHandler PropertyChanged;

        private bool GetIsDataDiagnosticsInfoBarEnabled()
        {
            var isDataDiagnosticsGpoDisallowed = GPOWrapper.GetAllowDataDiagnosticsValue() == GpoRuleConfigured.Disabled;

            return !isDataDiagnosticsGpoDisallowed;
        }

        public OobeOverview()
        {
            this.InitializeComponent();

            _enableDataDiagnostics = DataDiagnosticsSettings.GetEnabledValue();

            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.Overview]);
            DataContext = this;

            // Subscribe to hotkey conflict updates
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated += OnConflictsUpdated;
                GlobalHotkeyConflictManager.Instance.RequestAllConflicts();
            }
        }

        private void OnConflictsUpdated(object sender, AllHotkeyConflictsEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                AllHotkeyConflictsData = e.Conflicts ?? new AllHotkeyConflictsData();
            });
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(DashboardPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        private void GeneralSettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeShellPage.OpenMainWindowCallback != null)
            {
                OobeShellPage.OpenMainWindowCallback(typeof(GeneralPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        private void ShortcutConflictBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (AllHotkeyConflictsData == null || !HasConflicts)
            {
                return;
            }

            // Create and show the shortcut conflict window
            var conflictWindow = new ShortcutConflictWindow();
            conflictWindow.Activate();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();

            // Unsubscribe from conflict updates when leaving the page
            if (GlobalHotkeyConflictManager.Instance != null)
            {
                GlobalHotkeyConflictManager.Instance.ConflictsUpdated -= OnConflictsUpdated;
            }
        }
    }
}
