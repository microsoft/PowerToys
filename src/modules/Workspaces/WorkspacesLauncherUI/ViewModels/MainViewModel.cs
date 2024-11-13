// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ManagedCommon;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.Utils;

namespace WorkspacesLauncherUI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<AppLaunching> AppsListed { get; set; } = new ObservableCollection<AppLaunching>();

        private StatusWindow _snapshotWindow;
        private int launcherProcessID;
        private PwaHelper _pwaHelper;

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public MainViewModel()
        {
            _pwaHelper = new PwaHelper();

            // receive IPC Message
            App.IPCMessageReceivedCallback = (string msg) =>
            {
                try
                {
                    AppLaunchData parser = new AppLaunchData();
                    AppLaunchData.AppLaunchDataWrapper appLaunchData = parser.Deserialize(msg);
                    HandleAppLaunchingState(appLaunchData);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.Message);
                }
            };
        }

        private void HandleAppLaunchingState(AppLaunchData.AppLaunchDataWrapper appLaunchData)
        {
            launcherProcessID = appLaunchData.LauncherProcessID;
            List<AppLaunching> appLaunchingList = new List<AppLaunching>();
            foreach (var app in appLaunchData.AppLaunchInfos.AppLaunchInfoList)
            {
                appLaunchingList.Add(new AppLaunching()
                {
                    Application = app.Application,
                    LaunchState = app.State,
                });
            }

            AppsListed = new ObservableCollection<AppLaunching>(appLaunchingList);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppsListed)));
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
            App.SendIPCMessage("cancel");
        }

        internal static string GetPwaIconFilename(AppLaunching appLaunching)
        {
            if (appLaunching.IsEdge)
            {
                return PwaHelper.GetEdgeAppIconFile(appLaunching.Application.PwaAppId);
            }
            else if (appLaunching.IsChrome)
            {
                return PwaHelper.GetChromeAppIconFile(appLaunching.Application.PwaAppId);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
