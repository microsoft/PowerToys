// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Web.AtomPub;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.SettingsXAML.Controls.Dashboard
{
    public sealed partial class ShortcutConflictWindow : WindowEx
    {
        public ShortcutConflictViewModel DataContext { get; }

        public ShortcutConflictViewModel ViewModel { get; private set; }

        public ShortcutConflictWindow()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new ShortcutConflictViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);

            DataContext = ViewModel;
            InitializeComponent();

            // Set up the custom action name delegate for LocalizationHelper
            LocalizationHelper.GetCustomActionNameDelegate = GetCustomActionName;

            // Set localized window title
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            this.ExtendsContentIntoTitleBar = true;

            this.CenterOnScreen();

            ViewModel.OnPageLoaded();
        }

        private void CenterOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var windowSize = this.AppWindow.Size;
                var centeredPosition = new PointInt32
                {
                    X = (displayArea.WorkArea.Width - windowSize.Width) / 2,
                    Y = (displayArea.WorkArea.Height - windowSize.Height) / 2,
                };
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard settingsCard &&
                settingsCard.DataContext is ModuleHotkeyData moduleData)
            {
                var moduleName = moduleData.ModuleName;

                // Navigate to the module's settings page
                if (ModuleNavigationHelper.NavigateToModulePage(moduleName))
                {
                    this.Close();
                }
            }
        }

        /// <summary>
        /// Gets the custom action name for AdvancedPaste
        /// </summary>
        /// <param name="moduleName">The module name</param>
        /// <param name="actionId">The custom action ID</param>
        /// <returns>The custom action name, or null if not found</returns>
        private string GetCustomActionName(string moduleName, int actionId)
        {
            if (!moduleName.Equals("advancedpaste", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return ViewModel?.GetAdvancedPasteCustomActionName(actionId);
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args)
        {
            LocalizationHelper.GetCustomActionNameDelegate = null;
            ViewModel?.Dispose();
        }
    }
}
