// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace AdvancedPaste.Settings
{
    internal sealed class UserSettings : IUserSettings, IDisposable
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly TaskScheduler _taskScheduler;
        private readonly IFileSystemWatcher _watcher;
        private readonly object _loadingSettingsLock = new();

        private const string AdvancedPasteModuleName = "AdvancedPaste";
        private const int MaxNumberOfRetry = 5;

        private bool _disposedValue;
        private CancellationTokenSource _cancellationTokenSource;

        public bool ShowCustomPreview { get; private set; }

        public bool SendPasteKeyCombination { get; private set; }

        public bool CloseAfterLosingFocus { get; private set; }

        public ObservableCollection<AdvancedPasteCustomAction> CustomActions { get; private set; }

        public UserSettings()
        {
            _settingsUtils = new SettingsUtils();

            ShowCustomPreview = true;
            SendPasteKeyCombination = true;
            CloseAfterLosingFocus = false;
            CustomActions = [];

            _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(AdvancedPasteModuleName, "settings.json", OnSettingsFileChanged);
        }

        private void OnSettingsFileChanged()
        {
            lock (_loadingSettingsLock)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                Task.Delay(TimeSpan.FromMilliseconds(500))
                    .ContinueWith(_ => LoadSettingsFromJson(), _cancellationTokenSource.Token, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);
            }
        }

        private void LoadSettingsFromJson()
        {
            lock (_loadingSettingsLock)
            {
                var retry = true;
                var retryCount = 0;

                while (retry)
                {
                    try
                    {
                        retryCount++;

                        if (!_settingsUtils.SettingsExists(AdvancedPasteModuleName))
                        {
                            Logger.LogInfo("AdvancedPaste settings.json was missing, creating a new one");
                            var defaultSettings = new AdvancedPasteSettings();
                            defaultSettings.Save(_settingsUtils);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(AdvancedPasteModuleName);
                        if (settings != null)
                        {
                            void UpdateSettings()
                            {
                                ShowCustomPreview = settings.Properties.ShowCustomPreview;
                                SendPasteKeyCombination = settings.Properties.SendPasteKeyCombination;
                                CloseAfterLosingFocus = settings.Properties.CloseAfterLosingFocus;

                                CustomActions.Clear();
                                foreach (var customAction in settings.Properties.CustomActions.Value)
                                {
                                    if (customAction.IsShown && customAction.IsValid)
                                    {
                                        CustomActions.Add(customAction);
                                    }
                                }
                            }

                            Task.Factory
                                .StartNew(UpdateSettings, CancellationToken.None, TaskCreationOptions.None, _taskScheduler)
                                .Wait();
                        }

                        retry = false;
                    }
                    catch (Exception ex)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                        }

                        Logger.LogError("Failed to read changed settings", ex);
                        Thread.Sleep(500);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Dispose();
                    _watcher.Dispose();
                }

                _disposedValue = true;
            }
        }

        ~UserSettings()
        {
            Dispose(false);
        }
    }
}
