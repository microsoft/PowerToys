// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Dispatching;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class UpdateViewModel : Observable, IDisposable
    {
        public enum UpdateUIState
        {
            UpToDate = 0,
            Checking,
            NetworkError,
            ReadyToDownload,
            Downloading,
            ReadyToInstall,
            ErrorDownloading,
        }

        internal enum TransientUpdateOperation
        {
            None,
            Checking,
            Downloading,
            Installing,
        }

        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
        private readonly Func<string, int> _sendCheckForUpdatesConfigMessage;
        private readonly Func<UpdatingSettings> _loadSettings;
        private readonly Action _startUpdate;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DispatcherQueueTimer _updateCheckTimeoutTimer;
        private readonly bool _isDevBuild;
        private IFileSystemWatcher _fileWatcher;
        private UpdatingSettings _updatingSettings;
        private TransientUpdateOperation _activeUpdateOperation;
        private UpdateUIState? _transientFailureState;
        private bool _isActivityRequested;
        private bool _isActivityDismissed;
        private bool _disposed;

#if DEBUG
        private UpdateUIState? _debugPreviewState;
#endif

        public UpdateViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> sendCheckForUpdatesConfigMessage)
            : this(
                  settingsRepository,
                  sendCheckForUpdatesConfigMessage,
                  UpdatingSettings.LoadSettings,
                  StartUpdate,
                  Helper.GetProductVersion() == "v0.0.1",
                  true,
                  DispatcherQueue.GetForCurrentThread())
        {
        }

        internal UpdateViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> sendCheckForUpdatesConfigMessage,
            Func<UpdatingSettings> loadSettings,
            Action startUpdate,
            bool isDevBuild,
            bool watchForChanges,
            DispatcherQueue dispatcherQueue)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            ArgumentNullException.ThrowIfNull(sendCheckForUpdatesConfigMessage);
            ArgumentNullException.ThrowIfNull(loadSettings);
            ArgumentNullException.ThrowIfNull(startUpdate);

            _settingsRepository = settingsRepository;
            _sendCheckForUpdatesConfigMessage = sendCheckForUpdatesConfigMessage;
            _loadSettings = loadSettings;
            _startUpdate = startUpdate;
            _dispatcherQueue = dispatcherQueue;
            _isDevBuild = isDevBuild;
            _updatingSettings = _loadSettings() ?? new UpdatingSettings();

            if (_dispatcherQueue is not null)
            {
                _updateCheckTimeoutTimer = _dispatcherQueue.CreateTimer();
                _updateCheckTimeoutTimer.Interval = TimeSpan.FromMinutes(2);
                _updateCheckTimeoutTimer.IsRepeating = false;
                _updateCheckTimeoutTimer.Tick += UpdateCheckTimeoutTimer_Tick;
            }

            CheckForUpdatesCommand = new RelayCommand(CheckForUpdates, () => CanStartAction);
            UpdateNowCommand = new RelayCommand(UpdateNow, () => CanStartAction);
            PrimaryActionCommand = new RelayCommand(ExecutePrimaryAction, () => CanStartAction);

            if (watchForChanges)
            {
                _fileWatcher = Helper.GetFileWatcher(string.Empty, UpdatingSettings.SettingsFile, OnUpdateStateFileChanged);
            }
        }

        public RelayCommand CheckForUpdatesCommand { get; }

        public RelayCommand UpdateNowCommand { get; }

        public RelayCommand PrimaryActionCommand { get; }

        public UpdateUIState CurrentUpdateUIState
        {
            get
            {
#if DEBUG
                if (_debugPreviewState.HasValue)
                {
                    return _debugPreviewState.Value;
                }
#endif
                return _transientFailureState ?? GetUpdateUIState(_updatingSettings.State, _activeUpdateOperation);
            }
        }

        public string StatusTitle
        {
            get
            {
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                return CurrentUpdateUIState switch
                {
                    UpdateUIState.Checking => resourceLoader.GetString("General_CheckingForUpdates/Text"),
                    UpdateUIState.NetworkError => resourceLoader.GetString("General_CantCheck/Title"),
                    UpdateUIState.ReadyToDownload or
                    UpdateUIState.ReadyToInstall => resourceLoader.GetString("General_UpdateAvailableTitle"),
                    UpdateUIState.Downloading => resourceLoader.GetString("General_Downloading/Text"),
                    UpdateUIState.ErrorDownloading => resourceLoader.GetString("General_FailedToDownloadTheNewVersion/Title"),
                    _ => resourceLoader.GetString("General_UpToDate/Title"),
                };
            }
        }

        public string StatusDescription
        {
            get
            {
                if (CurrentUpdateUIState is UpdateUIState.ReadyToDownload or
                    UpdateUIState.Downloading or
                    UpdateUIState.ReadyToInstall or
                    UpdateUIState.ErrorDownloading)
                {
                    return DisplayVersion;
                }

                var lastCheckedDate = FriendlyDateHelper.Format(_updatingSettings.LastCheckedDateTime);
                if (string.IsNullOrEmpty(lastCheckedDate))
                {
                    return string.Empty;
                }

                return ResourceLoaderInstance.ResourceLoader.GetString("General_VersionLastChecked/Text") + lastCheckedDate;
            }
        }

        public string DisplayVersion
        {
            get
            {
#if DEBUG
                if (_debugPreviewState.HasValue)
                {
                    return "v0.99.0";
                }
#endif
                return _updatingSettings.NewVersion;
            }
        }

        public string ReleasePageLink => _updatingSettings.ReleasePageLink;

        public bool IsPrereleaseUpdate => _updatingSettings.IsPrerelease;

        public bool IsProgressActive => CurrentUpdateUIState is UpdateUIState.Checking or UpdateUIState.Downloading;

        public string PrimaryActionText
        {
            get
            {
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                return CurrentUpdateUIState switch
                {
                    UpdateUIState.ReadyToDownload or UpdateUIState.Downloading => resourceLoader.GetString("General_DownloadAndInstall/Content"),
                    UpdateUIState.ReadyToInstall => resourceLoader.GetString("General_InstallNow/Content"),
                    UpdateUIState.ErrorDownloading => resourceLoader.GetString("General_TryAgainToDownloadAndInstall/Content"),
                    _ => resourceLoader.GetString("GeneralPage_CheckForUpdates/Content"),
                };
            }
        }

        public bool ShowReleaseLink => CurrentUpdateUIState is
            UpdateUIState.ReadyToDownload or
            UpdateUIState.Downloading or
            UpdateUIState.ReadyToInstall or
            UpdateUIState.ErrorDownloading;

        public bool ShowPrereleaseBadge => ShowReleaseLink && IsPrereleaseUpdate;

        public bool CanStartAction
        {
            get
            {
#if DEBUG
                if (_debugPreviewState.HasValue)
                {
                    return !IsProgressActive;
                }
#endif
                return !_isDevBuild && _activeUpdateOperation == TransientUpdateOperation.None;
            }
        }

        public bool IsActivityVisible =>
            _isActivityRequested ||
            (!_isActivityDismissed && CurrentUpdateUIState != UpdateUIState.UpToDate);

        public bool ShowUpdateBadge => CurrentUpdateUIState is
            UpdateUIState.ReadyToDownload or
            UpdateUIState.Downloading or
            UpdateUIState.ReadyToInstall or
            UpdateUIState.ErrorDownloading;

        internal static UpdateUIState GetUpdateUIState(
            UpdatingSettings.UpdatingState updatingState,
            TransientUpdateOperation activeUpdateOperation)
        {
            if (activeUpdateOperation == TransientUpdateOperation.Checking)
            {
                return UpdateUIState.Checking;
            }

            if (activeUpdateOperation == TransientUpdateOperation.Downloading)
            {
                return UpdateUIState.Downloading;
            }

            if (activeUpdateOperation == TransientUpdateOperation.Installing)
            {
                return UpdateUIState.ReadyToInstall;
            }

            return updatingState switch
            {
                UpdatingSettings.UpdatingState.NetworkError => UpdateUIState.NetworkError,
                UpdatingSettings.UpdatingState.ReadyToDownload => UpdateUIState.ReadyToDownload,
                UpdatingSettings.UpdatingState.ReadyToInstall => UpdateUIState.ReadyToInstall,
                UpdatingSettings.UpdatingState.ErrorDownloading => UpdateUIState.ErrorDownloading,
                _ => UpdateUIState.UpToDate,
            };
        }

        public void RequestActivity()
        {
            if (!_isActivityRequested || _isActivityDismissed)
            {
                _isActivityRequested = true;
                _isActivityDismissed = false;
                OnPropertyChanged(nameof(IsActivityVisible));
            }
        }

        public void DismissActivity()
        {
            if (_isActivityRequested || !_isActivityDismissed)
            {
                _isActivityRequested = false;
                _isActivityDismissed = true;
                OnPropertyChanged(nameof(IsActivityVisible));
            }
        }

        public void BeginWindowSession()
        {
            bool wasVisible = IsActivityVisible;
            _isActivityRequested = false;
            _isActivityDismissed = false;

            if (IsActivityVisible != wasVisible)
            {
                OnPropertyChanged(nameof(IsActivityVisible));
            }
        }

        public void CheckForUpdates()
        {
#if DEBUG
            if (_debugPreviewState.HasValue)
            {
                SetDebugPreviewState(UpdateUIState.Checking);
                return;
            }
#endif
            if (!CanStartAction)
            {
                Logger.LogWarning("An update operation is already in progress.");
                return;
            }

            var checkForUpdatesAction = JsonSerializer.Serialize(
                ActionMessage.Create("check_for_updates"),
                SourceGenerationContextContext.Default.ActionMessage);

            RequestActivity();
            StartTransientOperation(TransientUpdateOperation.Checking);
            try
            {
                if (_sendCheckForUpdatesConfigMessage(checkForUpdatesAction) != 0)
                {
                    FailTransientOperation(UpdateUIState.NetworkError, "Failed to send the update check request.");
                }
            }
            catch (Exception ex)
            {
                FailTransientOperation(UpdateUIState.NetworkError, "Failed to send the update check request.", ex);
            }
        }

        public void UpdateNow()
        {
#if DEBUG
            if (_debugPreviewState.HasValue)
            {
                SetDebugPreviewState(CurrentUpdateUIState == UpdateUIState.ReadyToInstall
                    ? UpdateUIState.UpToDate
                    : UpdateUIState.Downloading);
                return;
            }
#endif
            if (!CanStartAction)
            {
                Logger.LogWarning("An update operation is already in progress.");
                return;
            }

            RequestActivity();
            StartTransientOperation(string.IsNullOrEmpty(_updatingSettings.DownloadedInstallerFilename)
                ? TransientUpdateOperation.Downloading
                : TransientUpdateOperation.Installing);

            try
            {
                _startUpdate();
            }
            catch (Exception ex)
            {
                FailTransientOperation(UpdateUIState.ErrorDownloading, "Failed to start the PowerToys update.", ex);
            }
        }

        private void ExecutePrimaryAction()
        {
            if (CurrentUpdateUIState is UpdateUIState.UpToDate or UpdateUIState.Checking or UpdateUIState.NetworkError)
            {
                CheckForUpdates();
            }
            else
            {
                UpdateNow();
            }
        }

        internal void RefreshUpdatingState()
        {
            var updatingSettings = LoadSettingsWithRetry();
            if (updatingSettings == null)
            {
                Logger.LogWarning("Failed to load the PowerToys update state.");
                HandleUpdateStateRefreshFailure();
                return;
            }

            ApplyUpdatingSettings(updatingSettings);
        }

#if DEBUG
        internal bool IsPreviewing => _debugPreviewState.HasValue;

        internal void SetDebugPreviewState(UpdateUIState? state)
        {
            if (_debugPreviewState != state)
            {
                _debugPreviewState = state;
                CompleteTransientOperation();
                _transientFailureState = null;
                NotifyStateChanged();
            }
        }
#endif

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_updateCheckTimeoutTimer is not null)
            {
                _updateCheckTimeoutTimer.Stop();
                _updateCheckTimeoutTimer.Tick -= UpdateCheckTimeoutTimer_Tick;
            }

            _fileWatcher?.Dispose();
            _fileWatcher = null;
            GC.SuppressFinalize(this);
        }

        private static void StartUpdate()
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Helper.GetPowerToysInstallationFolder(), "PowerToys.exe"))
            {
                Arguments = "powertoys://update_now/",
            });
        }

        private void OnUpdateStateFileChanged()
        {
            var updatingSettings = LoadSettingsWithRetry();
            if (updatingSettings == null)
            {
                Logger.LogWarning("Failed to load the PowerToys update state after it changed.");
                QueueUpdateStateRefreshFailure();
                return;
            }

            if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
            {
                ApplyUpdatingSettings(updatingSettings);
            }
            else if (!_dispatcherQueue.TryEnqueue(() => ApplyUpdatingSettings(updatingSettings)))
            {
                Logger.LogWarning("Failed to queue a PowerToys update state refresh.");
            }
        }

        private UpdatingSettings LoadSettingsWithRetry()
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var updatingSettings = _loadSettings();
                if (updatingSettings != null)
                {
                    return updatingSettings;
                }

                if (attempt < 3)
                {
                    Thread.Sleep(100);
                }
            }

            return null;
        }

        private void ApplyUpdatingSettings(UpdatingSettings updatingSettings)
        {
            if (_disposed)
            {
                return;
            }

            _updatingSettings = updatingSettings;
            CompleteTransientOperation();
            _transientFailureState = null;
            NotifyStateChanged();
        }

        private void StartTransientOperation(TransientUpdateOperation operation)
        {
            CompleteTransientOperation();
            _activeUpdateOperation = operation;
            _transientFailureState = null;

            // The runner persists update state only after an automatic download finishes,
            // so a check timeout is safe only when automatic downloads are disabled.
            if (operation == TransientUpdateOperation.Checking && !_settingsRepository.SettingsConfig.AutoDownloadUpdates)
            {
                _updateCheckTimeoutTimer?.Start();
            }

            NotifyStateChanged();
        }

        private void CompleteTransientOperation()
        {
            _updateCheckTimeoutTimer?.Stop();
            _activeUpdateOperation = TransientUpdateOperation.None;
        }

        private void FailTransientOperation(UpdateUIState failureState, string message, Exception exception = null)
        {
            if (exception is null)
            {
                Logger.LogError(message);
            }
            else
            {
                Logger.LogError(message, exception);
            }

            CompleteTransientOperation();
            _transientFailureState = failureState;
            NotifyStateChanged();
        }

        private void HandleUpdateStateRefreshFailure()
        {
            if (_activeUpdateOperation == TransientUpdateOperation.None)
            {
                return;
            }

            var failureState = _activeUpdateOperation == TransientUpdateOperation.Checking
                ? UpdateUIState.NetworkError
                : UpdateUIState.ErrorDownloading;
            FailTransientOperation(failureState, "The active PowerToys update operation could not refresh its state.");
        }

        private void QueueUpdateStateRefreshFailure()
        {
            if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
            {
                HandleUpdateStateRefreshFailure();
            }
            else if (!_dispatcherQueue.TryEnqueue(HandleUpdateStateRefreshFailure))
            {
                Logger.LogWarning("Failed to queue PowerToys update state error handling.");
            }
        }

        private void UpdateCheckTimeoutTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (_activeUpdateOperation == TransientUpdateOperation.Checking)
            {
                FailTransientOperation(UpdateUIState.NetworkError, "The PowerToys update check timed out.");
            }
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(CurrentUpdateUIState));
            OnPropertyChanged(nameof(StatusTitle));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(DisplayVersion));
            OnPropertyChanged(nameof(ReleasePageLink));
            OnPropertyChanged(nameof(IsPrereleaseUpdate));
            OnPropertyChanged(nameof(IsProgressActive));
            OnPropertyChanged(nameof(PrimaryActionText));
            OnPropertyChanged(nameof(ShowReleaseLink));
            OnPropertyChanged(nameof(ShowPrereleaseBadge));
            OnPropertyChanged(nameof(CanStartAction));
            OnPropertyChanged(nameof(IsActivityVisible));
            OnPropertyChanged(nameof(ShowUpdateBadge));
            CheckForUpdatesCommand.OnCanExecuteChanged();
            UpdateNowCommand.OnCanExecuteChanged();
            PrimaryActionCommand.OnCanExecuteChanged();
        }
    }
}
