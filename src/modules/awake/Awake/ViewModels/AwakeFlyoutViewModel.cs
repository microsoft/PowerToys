// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Awake.Core;
using Awake.Properties;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.ViewModels
{
    /// <summary>
    /// Backs the Awake tray flyout. Reads the current Awake state from <see cref="Manager"/>
    /// (which is the single source of truth) and delegates user actions back to the same
    /// <c>SetXxxKeepAwake</c> APIs that the old HMENU click handlers used.
    /// </summary>
    public sealed partial class AwakeFlyoutViewModel : ObservableObject, IDisposable
    {
        private static readonly CompositeFormat StatusTimedFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_STATUS_TIMED);

        private static readonly CompositeFormat StatusExpirableFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_STATUS_EXPIRABLE);

        private readonly SettingsUtils _settingsUtils;
        private bool _suppressApply;

        [ObservableProperty]
        private AwakeMode _mode;

        [ObservableProperty]
        private bool _keepDisplayOn;

        [ObservableProperty]
        private DateTimeOffset _expirationDate;

        [ObservableProperty]
        private TimeSpan _expirationTime;

        [ObservableProperty]
        private string _statusText = string.Empty;

        public bool KeepDisplayOnEnabled => Mode != AwakeMode.PASSIVE;

        public Microsoft.UI.Xaml.Visibility TimedSectionVisibility =>
            Mode == AwakeMode.TIMED ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ExpirableSectionVisibility =>
            Mode == AwakeMode.EXPIRABLE ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ExitButtonVisibility =>
            ShowExitButton ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public ObservableCollection<TimedPreset> TimedPresets { get; } = new();

        public bool ShowExitButton { get; }

        public AwakeFlyoutViewModel(SettingsUtils settingsUtils, bool startedFromPowerToys)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            // "Exit" only makes sense when Awake is running standalone (i.e. not managed by
            // the PowerToys runner). Matches the legacy TrayHelper behavior.
            ShowExitButton = !startedFromPowerToys;

            Manager.ModeChanged += OnManagerModeChanged;
            Refresh();
        }

        /// <summary>
        /// Re-reads the current Awake state from <see cref="Manager"/> and the
        /// on-disk settings (for <see cref="TimedPresets"/>) and updates all
        /// bindable properties. Safe to call repeatedly.
        /// </summary>
        public void Refresh()
        {
            try
            {
                _suppressApply = true;

                Mode = Manager.CurrentOperatingMode;
                KeepDisplayOn = Manager.IsDisplayOn;

                var expireAt = Manager.ExpireAt;
                if (expireAt <= DateTimeOffset.Now)
                {
                    expireAt = DateTimeOffset.Now.AddMinutes(30);
                }

                ExpirationDate = new DateTimeOffset(expireAt.Date, expireAt.Offset);
                ExpirationTime = expireAt.TimeOfDay;

                RefreshTimedPresets();
                UpdateStatusText();
            }
            finally
            {
                _suppressApply = false;
            }
        }

        /// <summary>
        /// Rebuilds the <see cref="TimedPresets"/> list from the user's
        /// <c>CustomTrayTimes</c> setting, falling back to the defaults if empty.
        /// </summary>
        private void RefreshTimedPresets()
        {
            TimedPresets.Clear();

            Dictionary<string, uint> options;
            try
            {
                var settings = _settingsUtils.GetSettings<AwakeSettings>(Core.Constants.AppName) ?? new AwakeSettings();
                options = settings.Properties.CustomTrayTimes;
                if (options is null || options.Count == 0)
                {
                    options = Manager.GetDefaultTrayOptions();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load custom tray times: {ex.Message}");
                options = Manager.GetDefaultTrayOptions();
            }

            uint? currentTimedSeconds = null;
            if (Mode == AwakeMode.TIMED)
            {
                try
                {
                    var s = _settingsUtils.GetSettings<AwakeSettings>(Core.Constants.AppName);
                    if (s is not null)
                    {
                        currentTimedSeconds = (s.Properties.IntervalHours * 3600) + (s.Properties.IntervalMinutes * 60);
                    }
                }
                catch
                {
                    // Best-effort selection highlighting; ignore failures.
                }
            }

            foreach (var kv in options)
            {
                var preset = new TimedPreset(kv.Key, kv.Value)
                {
                    IsSelected = currentTimedSeconds.HasValue && currentTimedSeconds.Value == kv.Value,
                };
                TimedPresets.Add(preset);
            }
        }

        private void OnManagerModeChanged(object? sender, EventArgs e)
        {
            // Manager raises this off the dispatcher; marshal back to the UI thread
            // via the app's main window dispatcher if we have one.
            var dq = AwakeApp.Current?.MainWindow?.DispatcherQueue;
            if (dq is not null)
            {
                dq.TryEnqueue(Refresh);
            }
            else
            {
                Refresh();
            }
        }

        partial void OnModeChanged(AwakeMode value)
        {
            // Notify the computed/derived properties so XAML one-way bindings refresh.
            OnPropertyChanged(nameof(KeepDisplayOnEnabled));
            OnPropertyChanged(nameof(TimedSectionVisibility));
            OnPropertyChanged(nameof(ExpirableSectionVisibility));

            if (_suppressApply)
            {
                UpdateStatusText();
                return;
            }

            ApplyMode(value);
            UpdateStatusText();
        }

        partial void OnKeepDisplayOnChanged(bool value)
        {
            if (_suppressApply)
            {
                return;
            }

            // SetDisplay toggles the persisted value when running under PT config; otherwise
            // it directly drives the executor. Either way it always re-applies the current mode.
            if (value != Manager.IsDisplayOn)
            {
                Manager.SetDisplay();
            }
        }

        private void ApplyMode(AwakeMode mode)
        {
            try
            {
                switch (mode)
                {
                    case AwakeMode.PASSIVE:
                        Manager.SetPassiveKeepAwake();
                        break;

                    case AwakeMode.INDEFINITE:
                        Manager.SetIndefiniteKeepAwake(KeepDisplayOn);
                        break;

                    case AwakeMode.TIMED:
                        var selected = TimedPresets.FirstOrDefault(p => p.IsSelected) ?? TimedPresets.FirstOrDefault();
                        if (selected is not null)
                        {
                            Manager.SetTimedKeepAwake(selected.Seconds, KeepDisplayOn);
                        }

                        break;

                    case AwakeMode.EXPIRABLE:
                        ApplyExpirableFromPickers();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"AwakeFlyoutViewModel.ApplyMode failed: {ex}");
            }
        }

        [RelayCommand]
        private void SelectTimedPreset(TimedPreset? preset)
        {
            if (preset is null)
            {
                return;
            }

            foreach (var p in TimedPresets)
            {
                p.IsSelected = ReferenceEquals(p, preset);
            }

            Manager.SetTimedKeepAwake(preset.Seconds, KeepDisplayOn);
            UpdateStatusText();
        }

        [RelayCommand]
        private void ApplyExpirable()
        {
            ApplyExpirableFromPickers();
            UpdateStatusText();
        }

        private void ApplyExpirableFromPickers()
        {
            var target = new DateTimeOffset(
                ExpirationDate.Year,
                ExpirationDate.Month,
                ExpirationDate.Day,
                ExpirationTime.Hours,
                ExpirationTime.Minutes,
                0,
                DateTimeOffset.Now.Offset);

            if (target <= DateTimeOffset.Now)
            {
                Logger.LogWarning("Expirable target is in the past; ignoring.");
                return;
            }

            Manager.SetExpirableKeepAwake(target, KeepDisplayOn);
        }

        [RelayCommand]
        private void OpenSettings()
        {
            try
            {
                SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.Awake);
            }
            catch (Exception ex)
            {
                Logger.LogError($"OpenSettings failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ExitAwake()
        {
            Logger.LogInfo("ExitAwake invoked from flyout");
            Manager.CompleteExit(0);
        }

        private void UpdateStatusText()
        {
            StatusText = Mode switch
            {
                AwakeMode.INDEFINITE => Resources.AWAKE_FLYOUT_STATUS_INDEFINITE,
                AwakeMode.TIMED => string.Format(
                    CultureInfo.CurrentCulture,
                    StatusTimedFormat,
                    TimedPresets.FirstOrDefault(p => p.IsSelected)?.Label ?? string.Empty),
                AwakeMode.EXPIRABLE => string.Format(
                    CultureInfo.CurrentCulture,
                    StatusExpirableFormat,
                    Manager.ExpireAt.ToString("g", CultureInfo.CurrentCulture)),
                _ => Resources.AWAKE_FLYOUT_STATUS_OFF,
            };
        }

        public void Dispose()
        {
            Manager.ModeChanged -= OnManagerModeChanged;
        }
    }
}
