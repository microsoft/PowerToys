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
        private readonly Lock _loadingSettingsLock = new();

        public bool SourceCodeWrapText { get; private set; }

        public bool SourceCodeTryFormat { get; private set; }

        public int SourceCodeFontSize { get; private set; }

        public bool SourceCodeStickyScroll { get; private set; }

        public bool SourceCodeMinimap { get; private set; }

        public long SourceCodeMaxFileSizeBytes { get; private set; }

        public PreviewSettings()
        {
            _settingsUtils = SettingsUtils.Default;
            SourceCodeWrapText = false;
            SourceCodeTryFormat = false;
            SourceCodeFontSize = 14;
            SourceCodeStickyScroll = true;
            SourceCodeMinimap = false;
            SourceCodeMaxFileSizeBytes = ToMaxFileSizeBytes(PeekPreviewSettings.DefaultSourceCodeMaxFileSize);

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekSettings.ModuleName, PeekPreviewSettings.FileName, () => LoadSettingsFromJson());
        }

        /// <summary>
        /// Converts the persisted preview cap (in kilobytes) to bytes, clamped to the range the settings UI allows.
        /// The NumberBox bounds only apply to UI input, so a hand-edited settings file could otherwise disable
        /// every preview (zero or negative) or remove the memory cap entirely (very large values).
        /// </summary>
        public static long ToMaxFileSizeBytes(int kilobytes)
        {
            return (long)Math.Clamp(kilobytes, PeekPreviewSettings.MinSourceCodeMaxFileSize, PeekPreviewSettings.MaxSourceCodeMaxFileSize) * 1024;
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
                            SourceCodeMinimap = settings.SourceCodeMinimap.Value;
                            SourceCodeMaxFileSizeBytes = ToMaxFileSizeBytes(settings.SourceCodeMaxFileSize.Value);
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
