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
        /// The current settings. Initially set to defaults.
        /// </summary>
        private PeekSettings _settings = new();

        private PeekSettings Settings
        {
            get => _settings;
            set
            {
                lock (_settingsLock)
                {
                    _settings = value;
                    CloseAfterLosingFocus = _settings.Properties.CloseAfterLosingFocus.Value;
                    ConfirmFileDelete = _settings.Properties.ConfirmFileDelete.Value;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether Peek closes automatically when the window loses focus.
        /// </summary>
        public bool CloseAfterLosingFocus { get; private set; }

        private bool _confirmFileDelete;

        /// <summary>
        /// Gets or sets a value indicating whether the user is prompted before a file is recycled.
        /// </summary>
        /// <remarks>The user will always be prompted when the file cannot be sent to the Recycle
        /// Bin and would instead be permanently deleted.</remarks>
        public bool ConfirmFileDelete
        {
            get => _confirmFileDelete;
            set
            {
                if (_confirmFileDelete != value)
                {
                    _confirmFileDelete = value;

                    // We write directly to the settings file. The Settings UI will pick detect
                    // this change via its file watcher and update accordingly. This is the only
                    // setting that is modified by Peek itself.
                    lock (_settingsLock)
                    {
                        _settings.Properties.ConfirmFileDelete.Value = _confirmFileDelete;
                        _settingsUtils.SaveSettings(_settings.ToJsonString(), PeekModuleName);
                    }
                }
            }
        }

        public UserSettings()
        {
            _settingsUtils = new SettingsUtils();

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekModuleName, SettingsUtils.DefaultFileName, LoadSettingsFromJson);
        }

        private void LoadSettingsFromJson()
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    Settings = _settingsUtils.GetSettingsOrDefault<PeekSettings>(PeekModuleName);
                    return;
                }
                catch (System.IO.IOException ex)
                {
                    Logger.LogError($"Peek settings load attempt {attempt} failed: {ex.Message}", ex);
                    if (attempt == MaxAttempts)
                    {
                        Logger.LogError($"Failed to load Peek settings after {MaxAttempts} attempts. Continuing with default settings.");
                        break;
                    }

                    // Exponential back-off then retry.
                    Thread.Sleep(CalculateRetryDelay(attempt));
                }
                catch (Exception ex)
                {
                    // Anything other than an IO exception is an immediate failure.
                    Logger.LogError($"Peek settings load failed, continuing with defaults: {ex.Message}", ex);
                }
            }

            Settings = new PeekSettings();
        }

        private static int CalculateRetryDelay(int attempt)
        {
            return (int)Math.Pow(2, attempt) * 100;
        }
    }
}
