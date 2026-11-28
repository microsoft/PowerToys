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
                    AlwaysOnTop = _settings.Properties.AlwaysOnTop.Value;
                    ShowTaskbarIcon = _settings.Properties.ShowTaskbarIcon.Value;
                    CloseAfterLosingFocus = _settings.Properties.CloseAfterLosingFocus.Value;
                    ConfirmFileDelete = _settings.Properties.ConfirmFileDelete.Value;
                    ShowFilePreviewTooltip = _settings.Properties.ShowFilePreviewTooltip.Value;
                    ReadMediaPlayback();
                }

                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Changed;

        /// <summary>
        /// Gets a value indicating whether Peek shows its window on the top of the stack.
        /// </summary>
        public bool AlwaysOnTop { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Peek shows its icon on the taskbar when activated.
        /// </summary>
        public bool ShowTaskbarIcon { get; private set; }

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

                    // We write directly to the settings file. The Settings UI will detect
                    // this change via its file watcher and update accordingly. Peek also
                    // writes the hidden media volume the same way.
                    lock (_settingsLock)
                    {
                        _settings.Properties.ConfirmFileDelete.Value = _confirmFileDelete;
                        _settingsUtils.SaveSettings(_settings.ToJsonString(), PeekModuleName);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the file metadata tooltip is shown when hovering over the Peek preview.
        /// </summary>
        public bool ShowFilePreviewTooltip { get; private set; }

        public double? MediaVolume { get; private set; }

        public bool? MediaMuted { get; private set; }

        public UserSettings()
        {
            _settingsUtils = SettingsUtils.Default;

            LoadSettingsFromJson();

            _watcher = Helper.GetFileWatcher(PeekModuleName, SettingsUtils.DefaultFileName, LoadSettingsFromJson);
        }

        public void RememberMediaPlayback(double volume, bool isMuted)
        {
            if (NormalizeVolume(volume) is not double normalized || (MediaVolume == normalized && MediaMuted == isMuted))
            {
                return;
            }

            lock (_settingsLock)
            {
                MediaVolume = normalized;
                MediaMuted = isMuted;
                _settings.Properties.MediaVolume = new DoubleProperty(normalized);
                _settings.Properties.MediaMuted = new BoolProperty(isMuted);
                _settingsUtils.SaveSettings(_settings.ToJsonString(), PeekModuleName);
            }
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

        private void ReadMediaPlayback()
        {
            DoubleProperty savedVolume = _settings.Properties.MediaVolume;
            MediaVolume = NormalizeVolume(savedVolume == null ? null : savedVolume.Value);
            BoolProperty savedMuted = _settings.Properties.MediaMuted;
            MediaMuted = savedMuted == null ? null : savedMuted.Value;
        }

        private static double? NormalizeVolume(double? volume)
        {
            if (volume is not double value || !double.IsFinite(value))
            {
                return null;
            }

            return Math.Clamp(value, 0d, 1d);
        }
    }
}
