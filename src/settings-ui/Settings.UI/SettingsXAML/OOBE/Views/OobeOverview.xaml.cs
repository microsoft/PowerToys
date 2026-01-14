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

            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.Overview);
            DataContext = this;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(DashboardPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        private void GeneralSettingsLaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(GeneralPage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }
    }
}
