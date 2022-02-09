// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using Common.UI;
using ManagedCommon;
using Microsoft.Toolkit.Uwp.Notifications;
using PowerLauncher.Helper;
using PowerLauncher.Plugin;
using PowerLauncher.ViewModel;
using Windows.UI.Notifications;
using Wox.Infrastructure.Image;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox
{
    public class PublicAPIInstance : IPublicAPI, IDisposable
    {
        private readonly SettingWindowViewModel _settingsVM;
        private readonly MainViewModel _mainVM;
        private readonly ThemeManager _themeManager;
        private bool _disposed;

        public event ThemeChangedHandler ThemeChanged;

        public PublicAPIInstance(SettingWindowViewModel settingsVM, MainViewModel mainVM, ThemeManager themeManager)
        {
            _settingsVM = settingsVM ?? throw new ArgumentNullException(nameof(settingsVM));
            _mainVM = mainVM ?? throw new ArgumentNullException(nameof(mainVM));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _themeManager.ThemeChanged += OnThemeChanged;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());

            ToastNotificationManagerCompat.OnActivated += args =>
            {
            };
        }

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query, requery);
        }

        public void RestartApp()
        {
            _mainVM.MainWindowVisibility = Visibility.Hidden;

            // we must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            SaveAppAllSettings();

            // Todo : Implement logic to restart this app.
            Environment.Exit(0);
        }

        public void CheckForNewUpdate()
        {
            // _settingsVM.UpdateApp();
        }

        public void SaveAppAllSettings()
        {
            _mainVM.Save();
            _settingsVM.Save();
            PluginManager.Save();
            ImageLoader.Save();
        }

        public void ReloadAllPluginData()
        {
            PluginManager.ReloadData();
        }

        public void ShowMsg(string title, string subTitle = "", string iconPath = "", bool useMainWindowAsOwner = true)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(subTitle, title);
            });
        }

        public void ShowNotification(string text, string secondaryText = null)
        {
            var builder = new ToastContentBuilder().AddText(text);

            if (!string.IsNullOrWhiteSpace(secondaryText))
            {
                builder.AddText(secondaryText);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = new ToastNotification(builder.GetToastContent().GetXml());
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
            });
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins.ToList();
        }

        public Theme GetCurrentTheme()
        {
            return _themeManager.GetCurrentTheme();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            ThemeChanged?.Invoke(oldTheme, newTheme);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _themeManager.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
