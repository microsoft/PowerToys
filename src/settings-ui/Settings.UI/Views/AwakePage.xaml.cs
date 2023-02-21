// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AwakePage : Page, IRefreshablePage
    {
        private readonly string _appName = "Awake";
        private readonly SettingsUtils _settingsUtils;
        private readonly DispatcherQueue _dispatcherQueue;

        private readonly IFileSystem _fileSystem;
        private readonly IFileSystemWatcher _fileSystemWatcher;

        private AwakeViewModel ViewModel { get; set; }

        public AwakePage()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _fileSystem = new FileSystem();
            _settingsUtils = new SettingsUtils();

            ViewModel = new AwakeViewModel(SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils), SettingsRepository<AwakeSettings>.GetInstance(_settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;

            var settingsPath = _settingsUtils.GetSettingsFilePath(_appName);

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.CreateNew();
            _fileSystemWatcher.Path = _fileSystem.Path.GetDirectoryName(settingsPath);
            _fileSystemWatcher.Filter = _fileSystem.Path.GetFileName(settingsPath);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileSystemWatcher.Changed += Settings_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;

            InitializeComponent();
        }

        private void Settings_Changed(object sender, FileSystemEventArgs e)
        {
            bool taskAdded = _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                ViewModel = new AwakeViewModel(SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils), SettingsRepository<AwakeSettings>.GetInstance(_settingsUtils), ShellPage.SendDefaultIPCMessage);
                this.Bindings.Update();
            });

            Console.WriteLine("Task added.");
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
