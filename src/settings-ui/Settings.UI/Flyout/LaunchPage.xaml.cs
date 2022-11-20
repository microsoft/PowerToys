// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Flyout
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using interop;
    using Microsoft.PowerToys.Settings.UI.Library;
    using Microsoft.PowerToys.Settings.UI.ViewModels;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    public sealed partial class LaunchPage : Page
    {
        private FlyoutViewModel ViewModel { get; set; }

        public LaunchPage()
        {
            this.InitializeComponent();
            var settingsUtils = new SettingsUtils();
            ViewModel = new FlyoutViewModel(SettingsRepository<GeneralSettings>.GetInstance(settingsUtils));
            DataContext = ViewModel;
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            Button selectedButton = sender as Button;
            Frame selectedFrame = this.Parent as Frame;
        }

        private void ModuleButton_Click(object sender, RoutedEventArgs e)
        {
            Button selectedModuleBtn = sender as Button;
            switch ((string)selectedModuleBtn.Tag)
            {
                case "ColorPicker": // Launch ColorPicker
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "FancyZones": // Launch FancyZones Editor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FZEToggleEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "PowerLauncher": // Launch Run
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "MeasureTool": // Launch Screen Ruler
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.MasureToolTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "ShortcutGuide": // Launch Shortcut Guide
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShortcutGuideTriggerEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "PowerOCR": // Launch Text Extractor
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
            }
        }
    }
}
