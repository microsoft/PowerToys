// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class MouseUtilsPage : NavigablePage, IRefreshablePage
    {
        private MouseUtilsViewModel ViewModel { get; set; }

        // CursorBrake writes detected-edges.json from its own process, so the edge editor follows the
        // file. The watcher fires several times per write, and each fire rereads it. It lives as long
        // as the page is loaded: a page left behind holds a settings object that later saves do not
        // read, so its editor must not prune and save over newer settings.
        private IFileSystemWatcher cursorBrakeEdgesWatcher;

        public MouseUtilsPage()
        {
            try
            {
                // By mistake, the first release of Find My Mouse was saving settings in two places at the same time.
                // Delete the wrong path for Find My Mouse settings.
                var tempSettingsUtils = SettingsUtils.Default;
                if (tempSettingsUtils.SettingsExists("Find My Mouse"))
                {
                    var settingsFilePath = tempSettingsUtils.GetSettingsFilePath("Find My Mouse");
                    System.IO.File.Delete(settingsFilePath);
                    tempSettingsUtils.DeleteSettings("Find My Mouse");
                }
            }
            catch (System.Exception)
            {
            }

            var settingsUtils = SettingsUtils.Default;
            ViewModel = new MouseUtilsViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<AutoHideCursorSettings>.GetInstance(settingsUtils),
                SettingsRepository<FindMyMouseSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils),
                SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils),
                SettingsRepository<CursorWrapSettings>.GetInstance(settingsUtils),
                SettingsRepository<MouseButtonLockSettings>.GetInstance(settingsUtils),
                SettingsRepository<CursorBrakeSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);

            DataContext = ViewModel;
            InitializeComponent();

            this.MouseUtils_MouseJump_Panel.ViewModel = ViewModel;

            Loaded += (s, e) =>
            {
                WatchCursorBrakeEdges();
                ViewModel.OnPageLoaded();
            };
            Unloaded += (s, e) =>
            {
                cursorBrakeEdgesWatcher?.Dispose();
                cursorBrakeEdgesWatcher = null;
            };
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void WatchCursorBrakeEdges()
        {
            cursorBrakeEdgesWatcher ??= Helper.GetFileWatcher(
                CursorBrakeSettings.ModuleName,
                CursorBrakeDetectedEdges.FileName,
                () => DispatcherQueue.TryEnqueue(() =>
                {
                    if (cursorBrakeEdgesWatcher != null)
                    {
                        ViewModel.RefreshCursorBrakeEdges();
                    }
                }));
        }

        private void OpenAnimationsSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                StartProcessHelper.Start(StartProcessHelper.AnimationsSettings);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while trying to open the animations settings", ex);
            }
        }

        private void OpenMouseSettings_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            try
            {
                StartProcessHelper.Start(StartProcessHelper.MouseSettings);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while trying to open the mouse settings", ex);
            }
        }
    }
}
