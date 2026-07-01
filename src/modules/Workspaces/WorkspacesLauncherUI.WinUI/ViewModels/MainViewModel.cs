// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using ManagedCommon;
using WorkspacesCsharpLibrary;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Helpers;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly PwaHelper _pwaHelper;
        private bool _isDisposed;

        [ObservableProperty]
        private ObservableCollection<AppLaunching> _appsListed = new ObservableCollection<AppLaunching>();

        public MainViewModel()
        {
            _pwaHelper = new PwaHelper();

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
            List<AppLaunching> appLaunchingList = new List<AppLaunching>();
            foreach (var app in appLaunchData.AppLaunchInfos.AppLaunchInfoList)
            {
                appLaunchingList.Add(new AppLaunching()
                {
                    Name = app.Application.Application,
                    AppPath = app.Application.ApplicationPath,
                    IconImage = IconHelper.TryGetExecutableIcon(app.Application.ApplicationPath),
                    PackagedName = app.Application.PackageFullName,
                    Aumid = app.Application.AppUserModelId,
                    PwaAppId = app.Application.PwaAppId,
                    LaunchState = app.State,
                });
            }

            AppsListed = new ObservableCollection<AppLaunching>(appLaunchingList);
        }

        [RelayCommand]
        private void CancelLaunch()
        {
            App.SendIPCMessage("cancel");
        }

        [RelayCommand]
        private void Dismiss()
        {
            // Window close is handled by the view
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
