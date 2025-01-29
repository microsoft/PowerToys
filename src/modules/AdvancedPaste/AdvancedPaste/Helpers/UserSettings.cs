// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace AdvancedPaste.Settings
{
    internal sealed partial class UserSettings : IUserSettings, IDisposable
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly TaskScheduler _taskScheduler;
        private readonly IFileSystemWatcher _watcher;
        private readonly Lock _loadingSettingsLock = new();
        private readonly List<PasteFormats> _additionalActions;
        private readonly List<AdvancedPasteCustomAction> _customActions;

        private const string AdvancedPasteModuleName = "AdvancedPaste";
        private const int MaxNumberOfRetry = 5;

        private bool _disposedValue;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler Changed;

        public bool IsAdvancedAIEnabled { get; private set; }

        public bool ShowCustomPreview { get; private set; }

        public bool CloseAfterLosingFocus { get; private set; }

        public IReadOnlyList<PasteFormats> AdditionalActions => _additionalActions;

        public IReadOnlyList<AdvancedPasteCustomAction> CustomActions => _customActions;

        public UserSettings(IFileSystem fileSystem)
        {
            _settingsUtils = new SettingsUtils(fileSystem);

            IsAdvancedAIEnabled = false;
            ShowCustomPreview = true;
            CloseAfterLosingFocus = false;
            _additionalActions = [];
            _customActions = [];
            _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(AdvancedPasteModuleName, "settings.json", OnSettingsFileChanged, fileSystem);
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
                                var properties = settings.Properties;

                                IsAdvancedAIEnabled = properties.IsAdvancedAIEnabled;
                                ShowCustomPreview = properties.ShowCustomPreview;
                                CloseAfterLosingFocus = properties.CloseAfterLosingFocus;

                                var sourceAdditionalActions = properties.AdditionalActions;
                                (PasteFormats Format, IAdvancedPasteAction[] Actions)[] additionalActionFormats =
                                [
                                    (PasteFormats.ImageToText, [sourceAdditionalActions.ImageToText]),
                                    (PasteFormats.PasteAsTxtFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsTxtFile]),
                                    (PasteFormats.PasteAsPngFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsPngFile]),
                                    (PasteFormats.PasteAsHtmlFile, [sourceAdditionalActions.PasteAsFile, sourceAdditionalActions.PasteAsFile.PasteAsHtmlFile]),
                                    (PasteFormats.TranscodeToMp3, [sourceAdditionalActions.Transcode, sourceAdditionalActions.Transcode.TranscodeToMp3]),
                                    (PasteFormats.TranscodeToMp4, [sourceAdditionalActions.Transcode, sourceAdditionalActions.Transcode.TranscodeToMp4]),
                                ];

                                _additionalActions.Clear();
                                _additionalActions.AddRange(additionalActionFormats.Where(tuple => tuple.Actions.All(action => action.IsShown))
                                                                                   .Select(tuple => tuple.Format));

                                _customActions.Clear();
                                _customActions.AddRange(properties.CustomActions.Value.Where(customAction => customAction.IsShown && customAction.IsValid));

                                Changed?.Invoke(this, EventArgs.Empty);
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
                    _cancellationTokenSource?.Dispose();
                    _watcher?.Dispose();
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
