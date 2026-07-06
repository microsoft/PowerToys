// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
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
    /// Identifies which keep-awake duration the user has selected in the flyout. Shared across
    /// the launch page and the custom-time page so the selection survives frame navigation.
    /// </summary>
    public enum FlyoutSelectionKind
    {
        Timed,
        Forever,
        Custom,
        WhileApp,
        WhileAgent,
    }

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

        private static readonly CompositeFormat AwakeUntilFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_AWAKE_UNTIL);

        private static readonly CompositeFormat AwakeWhileAppFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_WHILE_APP_RUNS);

        private static readonly CompositeFormat AwakeWhileAgentFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_WHILE_AGENT_WORKS);

        private static readonly CompositeFormat CustomUntilCardFormat =
            CompositeFormat.Parse(Resources.AWAKE_FLYOUT_CARD_CUSTOM_UNTIL);

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
        private uint _intervalHours;

        [ObservableProperty]
        private uint _intervalMinutes;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _countdownTime = string.Empty;

        [ObservableProperty]
        private string _offAtText = string.Empty;

        [ObservableProperty]
        private Microsoft.UI.Xaml.Visibility _offAtVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveAppIconVisibility))]
        [NotifyPropertyChangedFor(nameof(ActiveCountdownVisibility))]
        private bool _isProcessBound;

        [ObservableProperty]
        private string _boundAppName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveAgentIconVisibility))]
        [NotifyPropertyChangedFor(nameof(ActiveCountdownVisibility))]
        private bool _isAgentBound;

        [ObservableProperty]
        private string _boundAgentName = string.Empty;

        [ObservableProperty]
        private string _customCardText = Resources.AWAKE_FLYOUT_CUSTOM;

        [ObservableProperty]
        private string _whileAppCardText = Resources.AWAKE_FLYOUT_CARD_WHILE_APP;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveAppIconVisibility))]
        [NotifyPropertyChangedFor(nameof(ActiveCountdownVisibility))]
        private Microsoft.UI.Xaml.Media.ImageSource? _whileAppCardIcon;

        // In the active header, show the bound app's icon (instead of the infinity glyph) whenever
        // Awake is tracking an app and we actually have an icon for it.
        public Microsoft.UI.Xaml.Visibility ActiveAppIconVisibility =>
            IsProcessBound && WhileAppCardIcon != null
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;

        // Agents have no bitmap icon, so surface a glyph in the header while agent-bound.
        public Microsoft.UI.Xaml.Visibility ActiveAgentIconVisibility =>
            IsAgentBound
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ActiveCountdownVisibility =>
            (IsProcessBound && WhileAppCardIcon != null) || IsAgentBound
                ? Microsoft.UI.Xaml.Visibility.Collapsed
                : Microsoft.UI.Xaml.Visibility.Visible;

        public bool KeepDisplayOnEnabled => Mode != AwakeMode.PASSIVE;

        // True while a keep-awake session is running; drives the header's active visual state.
        public bool IsActive => Mode != AwakeMode.PASSIVE;

        public Microsoft.UI.Xaml.Visibility StopButtonVisibility =>
            Mode != AwakeMode.PASSIVE ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility TimedSectionVisibility =>
            Mode == AwakeMode.TIMED ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility ExpirableSectionVisibility =>
            Mode == AwakeMode.EXPIRABLE ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public AwakeFlyoutViewModel(SettingsUtils settingsUtils, bool startedFromPowerToys)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            _ = startedFromPowerToys;

            Manager.ModeChanged += OnManagerModeChanged;
            Refresh();
        }

        /// <summary>
        /// Re-reads the current Awake state from <see cref="Manager"/> and the
        /// on-disk settings and updates all bindable properties. Safe to call repeatedly.
        /// </summary>
        public void Refresh()
        {
            try
            {
                _suppressApply = true;

                // Set the process-bound state *before* Mode. Setting Mode raises PropertyChanged
                // synchronously, and listeners (e.g. the launch page) re-run SyncPendingFromMode,
                // which needs the correct IsProcessBound to map INDEFINITE to "While app" vs. "Forever".
                IsProcessBound = Manager.IsProcessBound;
                BoundAppName = Manager.BoundProcessName;
                IsAgentBound = Manager.IsAgentBound;
                BoundAgentName = Manager.BoundAgentName;
                Mode = Manager.CurrentOperatingMode;
                KeepDisplayOn = Manager.IsDisplayOn;

                var expireAt = Manager.ExpireAt;
                if (expireAt <= DateTimeOffset.Now)
                {
                    expireAt = DateTimeOffset.Now.AddMinutes(30);
                }

                ExpirationDate = new DateTimeOffset(expireAt.Date, expireAt.Offset);
                ExpirationTime = expireAt.TimeOfDay;

                LoadIntervalFromSettings();
                UpdateStatusText();
                UpdateCountdown();
            }
            finally
            {
                _suppressApply = false;
            }
        }

        private void LoadIntervalFromSettings()
        {
            try
            {
                var settings = _settingsUtils.GetSettings<AwakeSettings>(Core.Constants.AppName);
                if (settings is not null)
                {
                    IntervalHours = settings.Properties.IntervalHours;
                    IntervalMinutes = settings.Properties.IntervalMinutes;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to load interval from settings: {ex.Message}");
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
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(StopButtonVisibility));
            OnPropertyChanged(nameof(TimedSectionVisibility));
            OnPropertyChanged(nameof(ExpirableSectionVisibility));

            if (_suppressApply)
            {
                UpdateStatusText();
                UpdateCountdown();
                return;
            }

            ApplyMode(value);
            UpdateStatusText();
            UpdateCountdown();
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

        partial void OnIntervalHoursChanged(uint value)
        {
            OnIntervalChanged();
        }

        partial void OnIntervalMinutesChanged(uint value)
        {
            OnIntervalChanged();
        }

        partial void OnExpirationDateChanged(DateTimeOffset value)
        {
            OnExpirationChanged();
        }

        partial void OnExpirationTimeChanged(TimeSpan value)
        {
            OnExpirationChanged();
        }

        private void OnIntervalChanged()
        {
            if (_suppressApply)
            {
                return;
            }

            if (Mode == AwakeMode.TIMED)
            {
                ApplyTimedFromInterval();
            }

            UpdateStatusText();
        }

        private void OnExpirationChanged()
        {
            if (_suppressApply)
            {
                return;
            }

            if (Mode == AwakeMode.EXPIRABLE)
            {
                ApplyExpirableFromPickers();
            }

            UpdateStatusText();
        }

        private void ApplyTimedFromInterval()
        {
            uint seconds = (IntervalHours * 3600u) + (IntervalMinutes * 60u);
            if (seconds == 0)
            {
                // 0/0 would resolve to an instantaneous expiration; ignore until the user
                // provides a non-zero interval.
                return;
            }

            Manager.SetTimedKeepAwake(seconds, KeepDisplayOn);
        }

        /// <summary>
        /// Applies a one-tap timed preset (e.g. the 30m / 1h / 2h / 4h chips). Switches into
        /// timed mode if necessary and starts the keep-awake session immediately, applying the
        /// hours/minutes in a single shot so we don't kick off two redundant timers.
        /// </summary>
        public void ApplyTimedPreset(uint hours, uint minutes)
        {
            try
            {
                _suppressApply = true;
                IntervalHours = hours;
                IntervalMinutes = minutes;
            }
            finally
            {
                _suppressApply = false;
            }

            // Setting Mode raises OnModeChanged which applies the timed interval; if we're
            // already in timed mode that path doesn't run, so apply directly.
            if (Mode != AwakeMode.TIMED)
            {
                Mode = AwakeMode.TIMED;
            }
            else
            {
                ApplyTimedFromInterval();
                UpdateStatusText();
            }

            UpdateCountdown();
        }

        /// <summary>
        /// Applies the custom "Until date" selection from the date/time pickers. Switches into
        /// expirable mode if necessary; if already expirable, re-applies so edited values take effect.
        /// </summary>
        public void ApplyUntilDate()
        {
            if (Mode != AwakeMode.EXPIRABLE)
            {
                Mode = AwakeMode.EXPIRABLE;
            }
            else
            {
                ApplyExpirableFromPickers();
                UpdateStatusText();
            }

            UpdateCountdown();
        }

        // The duration the user has selected in the flyout but may not have applied yet (the
        // Start button applies it). Kept on the view model so the launch page and the custom-time
        // page share one source of truth across frame navigation.
        public FlyoutSelectionKind PendingSelection { get; set; } = FlyoutSelectionKind.Timed;

        public uint PendingMinutes { get; set; } = 60;

        public bool PendingCustomIsUntil { get; set; }

        public int PendingProcessId { get; set; }

        public string PendingProcessName { get; set; } = string.Empty;

        public string PendingAgentId { get; set; } = string.Empty;

        public string PendingAgentName { get; set; } = string.Empty;

        /// <summary>
        /// Records a custom duration / until-date selection (without starting it) and updates the
        /// Custom card label. The session only starts when the user presses Start, except when a
        /// session is already running, in which case it is re-applied live.
        /// </summary>
        public void SetPendingCustom(bool isUntil)
        {
            PendingSelection = FlyoutSelectionKind.Custom;
            PendingCustomIsUntil = isUntil;
            CustomCardText = FormatCustomCardLabel();
            ApplyPendingIfActive();
        }

        /// <summary>
        /// Records a "while app runs" selection (without starting it) and updates the While-app
        /// card label/icon. Starts on Start, or re-applies live if a session is already running.
        /// </summary>
        public void SetPendingApp(int processId, string processName, Microsoft.UI.Xaml.Media.ImageSource? icon)
        {
            PendingSelection = FlyoutSelectionKind.WhileApp;
            PendingProcessId = processId;
            PendingProcessName = processName ?? string.Empty;
            WhileAppCardText = string.IsNullOrEmpty(processName) ? Resources.AWAKE_FLYOUT_CARD_WHILE_APP : processName;
            WhileAppCardIcon = icon;
            ApplyPendingIfActive();
        }

        /// <summary>
        /// Records a "while agent works" selection (without starting it). Reuses the While-app card
        /// as the surface: the card shows the agent's name and, on Start, binds keep-awake to the
        /// agent's activity via <see cref="Manager.SetAgentBoundKeepAwake"/>.
        /// </summary>
        public void SetPendingAgent(string agentId, string agentName, Microsoft.UI.Xaml.Media.ImageSource? icon = null)
        {
            PendingSelection = FlyoutSelectionKind.WhileAgent;
            PendingAgentId = agentId ?? string.Empty;
            PendingAgentName = agentName ?? string.Empty;
            WhileAppCardText = string.IsNullOrEmpty(agentName) ? Resources.AWAKE_FLYOUT_CARD_WHILE_APP : agentName;
            WhileAppCardIcon = icon;
            ApplyPendingIfActive();
        }

        private string FormatCustomCardLabel()
        {
            if (PendingCustomIsUntil)
            {
                var target = new DateTimeOffset(
                    ExpirationDate.Year,
                    ExpirationDate.Month,
                    ExpirationDate.Day,
                    ExpirationTime.Hours,
                    ExpirationTime.Minutes,
                    0,
                    DateTimeOffset.Now.Offset);

                string when = target.LocalDateTime.Date == DateTime.Now.Date
                    ? target.ToString("t", CultureInfo.CurrentCulture)
                    : target.ToString("g", CultureInfo.CurrentCulture);

                return string.Format(CultureInfo.CurrentCulture, CustomUntilCardFormat, when);
            }

            return FormatInterval(IntervalHours, IntervalMinutes);
        }

        /// <summary>
        /// Realigns <see cref="PendingSelection"/> with the running mode so the launch page
        /// highlights the matching card after a refresh or a fresh open. Non-preset durations and
        /// expirations map to the Custom card; a process binding maps to the While-app card.
        /// </summary>
        public void SyncPendingFromMode()
        {
            switch (Mode)
            {
                case AwakeMode.INDEFINITE when IsAgentBound:
                    PendingSelection = FlyoutSelectionKind.WhileAgent;
                    PendingAgentId = Manager.BoundAgentId;
                    PendingAgentName = BoundAgentName;
                    if (!string.IsNullOrEmpty(BoundAgentName))
                    {
                        WhileAppCardText = BoundAgentName;
                    }

                    break;

                case AwakeMode.INDEFINITE when IsProcessBound:
                    PendingSelection = FlyoutSelectionKind.WhileApp;
                    PendingProcessName = BoundAppName;
                    PendingProcessId = ProcessIdOrZero();
                    if (!string.IsNullOrEmpty(BoundAppName))
                    {
                        WhileAppCardText = BoundAppName;
                    }

                    break;

                case AwakeMode.INDEFINITE:
                    PendingSelection = FlyoutSelectionKind.Forever;
                    break;

                case AwakeMode.EXPIRABLE:
                    PendingSelection = FlyoutSelectionKind.Custom;
                    PendingCustomIsUntil = true;
                    CustomCardText = FormatCustomCardLabel();
                    break;

                case AwakeMode.TIMED:
                    uint minutes = (IntervalHours * 60) + IntervalMinutes;
                    if (minutes is 30 or 60 or 120)
                    {
                        PendingSelection = FlyoutSelectionKind.Timed;
                        PendingMinutes = minutes;
                    }
                    else
                    {
                        PendingSelection = FlyoutSelectionKind.Custom;
                        PendingCustomIsUntil = false;
                        CustomCardText = FormatInterval(IntervalHours, IntervalMinutes);
                    }

                    break;

                default:
                    PendingSelection = FlyoutSelectionKind.Timed;
                    PendingMinutes = 60;
                    break;
            }
        }

        private static int ProcessIdOrZero() => Manager.IsProcessBound ? Manager.ProcessId : 0;

        /// <summary>
        /// Refreshes only the Custom sub-mode (duration vs. until-date) from the running session so
        /// that reopening the custom page lands on the tab that matches the active mode. Leaves the
        /// flag untouched when nothing is running, preserving the user's last in-flyout choice.
        /// </summary>
        public void RefreshPendingCustomSubMode()
        {
            switch (Mode)
            {
                case AwakeMode.EXPIRABLE:
                    PendingCustomIsUntil = true;
                    break;
                case AwakeMode.TIMED:
                    PendingCustomIsUntil = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Applies the pending selection, starting (or restarting) a keep-awake session.
        /// </summary>
        public void ApplyPendingSelection()
        {
            switch (PendingSelection)
            {
                case FlyoutSelectionKind.Forever:
                    Mode = AwakeMode.INDEFINITE;
                    break;

                case FlyoutSelectionKind.WhileApp:
                    if (PendingProcessId != 0)
                    {
                        ApplyProcessBinding(PendingProcessId, PendingProcessName);
                    }

                    break;

                case FlyoutSelectionKind.WhileAgent:
                    if (!string.IsNullOrEmpty(PendingAgentId))
                    {
                        ApplyAgentBinding(PendingAgentId, PendingAgentName);
                    }

                    break;

                case FlyoutSelectionKind.Custom when PendingCustomIsUntil:
                    ApplyUntilDate();
                    break;

                case FlyoutSelectionKind.Custom:
                    ApplyTimedPreset(IntervalHours, IntervalMinutes);
                    break;

                default:
                    ApplyTimedPreset(PendingMinutes / 60, PendingMinutes % 60);
                    break;
            }
        }

        /// <summary>
        /// Re-applies the pending selection only when a session is already running, so changing
        /// the selection updates the live session without forcing a stop and restart.
        /// </summary>
        public void ApplyPendingIfActive()
        {
            if (Mode != AwakeMode.PASSIVE)
            {
                ApplyPendingSelection();
            }
        }

        /// <summary>
        /// Binds keep-awake to a running process: keep the system awake while the target app
        /// runs and automatically revert to passive when it exits. Delegates to
        /// <see cref="Manager.SetProcessBoundKeepAwake"/> (same mechanism as the CLI <c>--pid</c>
        /// path). <see cref="Manager.ModeChanged"/> triggers a <see cref="Refresh"/>, which reads
        /// the bound-process state back into the bindable properties.
        /// </summary>
        public void ApplyProcessBinding(int processId, string appName)
        {
            try
            {
                Manager.SetProcessBoundKeepAwake(processId, appName, KeepDisplayOn);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to bind keep-awake to process {processId}: {ex.Message}");
            }
        }

        public void ApplyAgentBinding(string agentId, string agentName)
        {
            try
            {
                Manager.SetAgentBoundKeepAwake(agentId, agentName, KeepDisplayOn);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to bind keep-awake to agent {agentId}: {ex.Message}");
            }
        }

        /// <see cref="Manager.ModeStartedAt"/> / <see cref="Manager.ExpireAt"/>. Intended to be
        /// called once per second by the flyout while it is visible.
        /// </summary>
        public void UpdateCountdown()
        {
            bool countdownMode = Mode == AwakeMode.TIMED || Mode == AwakeMode.EXPIRABLE;

            if (countdownMode && Manager.ExpireAt > DateTimeOffset.Now)
            {
                TimeSpan remaining = Manager.ExpireAt - DateTimeOffset.Now;

                CountdownTime = FormatRemaining(remaining);
                OffAtText = FormatAwakeUntil(Manager.ExpireAt);
                OffAtVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else if (Mode == AwakeMode.INDEFINITE)
            {
                // No finite end: surface the infinity glyph instead of a countdown.
                CountdownTime = "\u221E";
                OffAtText = IsAgentBound
                    ? string.Format(CultureInfo.CurrentCulture, AwakeWhileAgentFormat, BoundAgentName)
                    : IsProcessBound
                        ? string.Format(CultureInfo.CurrentCulture, AwakeWhileAppFormat, BoundAppName)
                        : Resources.AWAKE_FLYOUT_AWAKE_INDEFINITELY;
                OffAtVisibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                // Passive (off) or an already-expired session.
                CountdownTime = Resources.AWAKE_FLYOUT_MODE_OFF;
                OffAtText = string.Empty;
                OffAtVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Formats the big gauge value using compact unit suffixes (e.g. "5h 10m 10s"), showing
        /// only the relevant units so it stays readable and visibly ticks down.
        /// </summary>
        private static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining.TotalDays >= 1)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}d {1}h",
                    (int)remaining.TotalDays,
                    remaining.Hours);
            }

            if (remaining.TotalHours >= 1)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}h {1}m {2}s",
                    (int)remaining.TotalHours,
                    remaining.Minutes,
                    remaining.Seconds);
            }

            if (remaining.TotalMinutes >= 1)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}m {1}s",
                    remaining.Minutes,
                    remaining.Seconds);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}s",
                remaining.Seconds);
        }

        /// <summary>
        /// Builds the "Awake until …" line shown beneath the gauge. Uses a short time for sessions
        /// ending today and a full date+time otherwise so multi-day sessions are unambiguous.
        /// </summary>
        private static string FormatAwakeUntil(DateTimeOffset expireAt)
        {
            string when = expireAt.LocalDateTime.Date == DateTime.Now.Date
                ? expireAt.ToString("t", CultureInfo.CurrentCulture)
                : expireAt.ToString("g", CultureInfo.CurrentCulture);

            return string.Format(CultureInfo.CurrentCulture, AwakeUntilFormat, when);
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
                        ApplyTimedFromInterval();
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

        private void UpdateStatusText()
        {
            StatusText = Mode switch
            {
                AwakeMode.INDEFINITE => IsAgentBound
                    ? string.Format(CultureInfo.CurrentCulture, AwakeWhileAgentFormat, BoundAgentName)
                    : IsProcessBound
                        ? string.Format(CultureInfo.CurrentCulture, AwakeWhileAppFormat, BoundAppName)
                        : Resources.AWAKE_FLYOUT_STATUS_INDEFINITE,
                AwakeMode.TIMED => string.Format(
                    CultureInfo.CurrentCulture,
                    StatusTimedFormat,
                    FormatInterval(IntervalHours, IntervalMinutes)),
                AwakeMode.EXPIRABLE => string.Format(
                    CultureInfo.CurrentCulture,
                    StatusExpirableFormat,
                    Manager.ExpireAt.ToString("g", CultureInfo.CurrentCulture)),
                _ => Resources.AWAKE_FLYOUT_STATUS_OFF,
            };
        }

        private static string FormatInterval(uint hours, uint minutes)
        {
            if (hours > 0 && minutes > 0)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0}h {1}m", hours, minutes);
            }

            if (hours > 0)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0}h", hours);
            }

            return string.Format(CultureInfo.CurrentCulture, "{0}m", minutes);
        }

        public void Dispose()
        {
            Manager.ModeChanged -= OnManagerModeChanged;
        }
    }
}
