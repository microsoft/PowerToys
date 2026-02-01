// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class HotkeyLauncherPage : NavigablePage, IRefreshablePage
    {
        private readonly string appName = "HotkeyLauncher";
        private readonly SettingsUtils settingsUtils;
        private readonly Func<string, int> sendConfigMsg = ShellPage.SendDefaultIPCMessage;

        private readonly SettingsRepository<GeneralSettings> generalSettingsRepository;
        private readonly SettingsRepository<HotkeyLauncherSettings> moduleSettingsRepository;

        private readonly IFileSystem fileSystem;
        private readonly IFileSystemWatcher fileSystemWatcher;
        private readonly DispatcherQueue dispatcherQueue;
        private bool suppressViewModelUpdates;

        private HotkeyLauncherViewModel ViewModel { get; set; }

        public HotkeyLauncherPage()
        {
            this.settingsUtils = SettingsUtils.Default;
            this.sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            this.generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(this.settingsUtils);
            this.moduleSettingsRepository = SettingsRepository<HotkeyLauncherSettings>.GetInstance(this.settingsUtils);

            var moduleSettings = this.moduleSettingsRepository.SettingsConfig;

            this.ViewModel = new HotkeyLauncherViewModel(
                this.generalSettingsRepository,
                moduleSettings,
                ShellPage.SendDefaultIPCMessage);

            this.ViewModel.SubscribeToActionChanges();

            DataContext = this.ViewModel;

            var settingsPath = this.settingsUtils.GetSettingsFilePath(this.appName);

            this.dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.fileSystem = new FileSystem();

            this.fileSystemWatcher = this.fileSystem.FileSystemWatcher.New();
            this.fileSystemWatcher.Path = this.fileSystem.Path.GetDirectoryName(settingsPath);
            this.fileSystemWatcher.Filter = this.fileSystem.Path.GetFileName(settingsPath);
            this.fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            this.fileSystemWatcher.Changed += Settings_Changed;
            this.fileSystemWatcher.EnableRaisingEvents = true;

            this.InitializeComponent();
            Loaded += (s, e) => this.ViewModel.OnPageLoaded();
        }

        public void RefreshEnabledState()
        {
            this.ViewModel.RefreshEnabledState();
        }

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            this.ViewModel.AddAction();
        }

        private void RemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is HotkeyLauncherAction action)
            {
                this.ViewModel.RemoveAction(action);
            }
        }

        private void Settings_Changed(object sender, FileSystemEventArgs e)
        {
            this.dispatcherQueue.TryEnqueue(() =>
            {
                this.suppressViewModelUpdates = true;

                this.moduleSettingsRepository.ReloadSettings();

                this.suppressViewModelUpdates = false;
            });
        }
    }
}
