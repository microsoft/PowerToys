// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Peek.UI
{
    public class UserSettings : IUserSettings
    {
        private const string PeekModuleName = "Peek";
        private const int MaxAttempts = 4;

        private readonly SettingsUtils _settingsUtils;

        private readonly Lock _settingsLock = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Defined in helper called in constructor.")]
        private readonly IFileSystemWatcher _watcher;

        /// <summary>
        /// Gets a value indicating whether Peek closes automatically when the window loses focus.
        /// </summary>
        public bool CloseAfterLosingFocus { get; private set; }

        public UserSettings()
        {
            _settingsUtils = new SettingsUtils();

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekModuleName, SettingsUtils.DefaultFileName, LoadSettingsFromJson);
        }

        private void ApplySettings(PeekSettings settings)
        {
            lock (_settingsLock)
            {
                CloseAfterLosingFocus = settings.Properties.CloseAfterLosingFocus.Value;
            }
        }

        private void ApplyDefaultSettings()
        {
            ApplySettings(new PeekSettings());
        }

        private void LoadSettingsFromJson()
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    ApplySettings(_settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekModuleName));
                    return;
                }
                catch (System.IO.IOException ex)
                {
                    Logger.LogError($"Peek settings load attempt {attempt} failed: {ex.Message}", ex);
                    if (attempt == MaxAttempts)
                    {
                        Logger.LogError($"Failed to load Peek settings after {MaxAttempts} attempts. Continuing with default settings.");
                        ApplyDefaultSettings();
                        return;
                    }

                    // Exponential back-off then retry.
                    Thread.Sleep(CalculateRetryDelay(attempt));
                }
                catch (Exception ex)
                {
                    // Anything other than an IO exception is an immediate failure.
                    Logger.LogError($"Peek settings load failed, continuing with defaults: {ex.Message}", ex);
                    ApplyDefaultSettings();
                    return;
                }
            }
        }

        private static int CalculateRetryDelay(int attempt)
        {
            return (int)Math.Pow(2, attempt) * 100;
        }
    }
}
