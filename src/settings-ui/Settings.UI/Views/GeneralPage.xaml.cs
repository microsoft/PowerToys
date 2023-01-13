// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// General Settings Page.
    /// </summary>
    public sealed partial class GeneralPage : Page
    {
        private static DateTime OkToHideBackupAndRestoreMessageTime { get; set; }

        /// <summary>
        /// Gets or sets view model.
        /// </summary>
        public GeneralViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralPage"/> class.
        /// General Settings page constructor.
        /// </summary>
        public GeneralPage()
        {
            InitializeComponent();

            // Load string resources
            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            var settingsUtils = new SettingsUtils();

            Action stateUpdatingAction = () =>
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.RefreshUpdatingState();
                });
            };

            Action hideBackupAndRestoreMessageArea = () =>
            {
                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    const int messageShowTimeIs = 10000;

                    // in order to keep the message for about 5 seconds after the last call
                    // and not need any lock/thread-synch, use an OK-To-Hide time, and wait just a little longer than that.
                    OkToHideBackupAndRestoreMessageTime = DateTime.UtcNow.AddMilliseconds(messageShowTimeIs - 16);
                    await System.Threading.Tasks.Task.Delay(messageShowTimeIs);
                    if (DateTime.UtcNow > OkToHideBackupAndRestoreMessageTime)
                    {
                        ViewModel.HideBackupAndRestoreMessageArea();
                    }
                });
            };

            var doRefreshBackupRestoreStatus = new Action<int>(RefreshBackupRestoreStatus);

            ViewModel = new GeneralViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                loader.GetString("GeneralSettings_RunningAsAdminText"),
                loader.GetString("GeneralSettings_RunningAsUserText"),
                ShellPage.IsElevated,
                ShellPage.IsUserAnAdmin,
                UpdateUIThemeMethod,
                ShellPage.SendDefaultIPCMessage,
                ShellPage.SendRestartAdminIPCMessage,
                ShellPage.SendCheckForUpdatesIPCMessage,
                string.Empty,
                stateUpdatingAction,
                hideBackupAndRestoreMessageArea,
                doRefreshBackupRestoreStatus,
                PickSingleFolderDialog,
                loader);

            DataContext = ViewModel;

            doRefreshBackupRestoreStatus(100);
        }

        public static int UpdateUIThemeMethod(string themeName)
        {
            switch (themeName?.ToUpperInvariant())
            {
                case "LIGHT":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    break;
                case "DARK":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    break;
                case "SYSTEM":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    break;
                default:
                    Logger.LogError($"Unexpected theme name: {themeName}");
                    break;
            }

            App.HandleThemeChange();
            return 0;
        }

        private void OpenColorsSettings_Click(object sender, RoutedEventArgs e)
        {
            Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.ColorsSettings);
        }

        private void RefreshBackupRestoreStatus(int delayMs = 0)
        {
            Task.Run(() =>
            {
                if (delayMs > 0)
                {
                    Thread.Sleep(delayMs);
                }

                var settingsBackupAndRestoreUtils = SettingsBackupAndRestoreUtils.Instance;
                var results = settingsBackupAndRestoreUtils.DryRunBackup();
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    ViewModel.NotifyAllBackupAndRestoreProperties();
                });
            });
        }

        private void UpdateBackupAndRestoreStatusText(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            RefreshBackupRestoreStatus();
        }

        private async Task<string> PickSingleFolderDialog()
        {
            var openPicker = new FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            openPicker.FileTypeFilter.Add("*");
            var folder = await openPicker.PickSingleFolderAsync();
            return folder?.Path;
        }
    }
}
