// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using ManagedCommon;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<AppLaunching> AppsListed { get; set; } = new ObservableCollection<AppLaunching>();

        private IFileSystemWatcher _watcher;
        private System.Timers.Timer selfDestroyTimer;
        private StatusWindow _snapshotWindow;
        private int launcherProcessID;

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public MainViewModel()
        {
            LoadAppLaunchInfos();
            string fileName = Path.GetFileName(AppLaunchData.File);
            _watcher = Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper.GetFileWatcher("Workspaces", fileName, () => AppLaunchInfoStateChanged());
        }

        private void AppLaunchInfoStateChanged()
        {
            LoadAppLaunchInfos();
        }

        private void LoadAppLaunchInfos()
        {
            AppLaunchData parser = new AppLaunchData();
            if (!File.Exists(AppLaunchData.File))
            {
                Logger.LogWarning($"AppLaunchInfosData storage file not found: {AppLaunchData.File}");
                return;
            }

            AppLaunchData.AppLaunchDataWrapper appLaunchData = parser.Read(AppLaunchData.File);

            launcherProcessID = appLaunchData.LauncherProcessID;

            List<AppLaunching> appLaunchingList = new List<AppLaunching>();
            bool allLaunched = true;
            foreach (var app in appLaunchData.AppLaunchInfos.AppLaunchInfoList)
            {
                appLaunchingList.Add(new AppLaunching()
                {
                    Name = app.Name,
                    AppPath = app.Path,
                    LaunchState = app.State,
                });
                if (app.State != "launched")
                {
                    allLaunched = false;
                }
            }

            AppsListed = new ObservableCollection<AppLaunching>(appLaunchingList);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppsListed)));

            if (allLaunched)
            {
                selfDestroyTimer = new System.Timers.Timer();
                selfDestroyTimer.Interval = 1000;
                selfDestroyTimer.Elapsed += SelfDestroy;
                selfDestroyTimer.Start();
            }
        }

        private void SelfDestroy(object source, System.Timers.ElapsedEventArgs e)
        {
            _snapshotWindow.Dispatcher.Invoke(() =>
            {
                _snapshotWindow.Close();
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        internal void SetSnapshotWindow(StatusWindow snapshotWindow)
        {
            _snapshotWindow = snapshotWindow;
        }

        internal void CancelLaunch()
        {
            Process proc = Process.GetProcessById(launcherProcessID);
            proc.Kill();
        }
    }
}
