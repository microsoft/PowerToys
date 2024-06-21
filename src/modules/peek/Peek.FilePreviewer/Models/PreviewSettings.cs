// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Settings.UI.Library;

namespace Peek.FilePreviewer.Models
{
    public class PreviewSettings : IPreviewSettings
    {
        private const int MaxNumberOfRetry = 5;

        private readonly SettingsUtils _settingsUtils;
        private readonly IFileSystemWatcher _watcher;
        private readonly object _loadingSettingsLock = new();

        public bool SourceCodeWrapText { get; private set; }

        public bool SourceCodeTryFormat { get; private set; }

        public int SourceCodeFontSize { get; private set; }

        public bool SourceCodeStickyScroll { get; private set; }

        public PreviewSettings()
        {
            _settingsUtils = new SettingsUtils();
            SourceCodeWrapText = false;
            SourceCodeTryFormat = false;
            SourceCodeFontSize = 14;
            SourceCodeStickyScroll = true;

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekSettings.ModuleName, PeekPreviewSettings.FileName, () => LoadSettingsFromJson());
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

                        if (!_settingsUtils.SettingsExists(PeekSettings.ModuleName, PeekPreviewSettings.FileName))
                        {
                            Logger.LogInfo("Peek preview-settings.json was missing, creating a new one");
                            var defaultSettings = new PeekPreviewSettings();
                            _settingsUtils.SaveSettings(defaultSettings.ToJsonString(), PeekSettings.ModuleName, PeekPreviewSettings.FileName);
                        }

                        var settings = _settingsUtils.GetSettingsOrDefault<PeekPreviewSettings>(PeekSettings.ModuleName, PeekPreviewSettings.FileName);
                        if (settings != null)
                        {
                            SourceCodeWrapText = settings.SourceCodeWrapText.Value;
                            SourceCodeTryFormat = settings.SourceCodeTryFormat.Value;
                            SourceCodeFontSize = settings.SourceCodeFontSize.Value;
                            SourceCodeStickyScroll = settings.SourceCodeStickyScroll.Value;
                        }

                        retry = false;
                    }
                    catch (IOException e)
                    {
                        if (retryCount > MaxNumberOfRetry)
                        {
                            retry = false;
                            Logger.LogError($"Failed to deserialize preview settings, Retrying {e.Message}", e);
                        }
                        else
                        {
                            Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        retry = false;
                        Logger.LogError("Failed to read changed preview settings", ex);
                    }
                }
            }
        }
    }
}
