// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// General Settings Page.
    /// </summary>
    public sealed partial class GeneralPage : NavigablePage, IRefreshablePage, IDisposable
    {
        private DateTime OkToHideBackupAndRestoreMessageTime { get; set; }

        private readonly PageViewModelLifetime<GeneralViewModel> _viewModelLifetime;
        private Action<JsonObject> _bugReportStatusHandler;

        /// <summary>
        /// Gets the view model.
        /// </summary>
        public GeneralViewModel ViewModel => _viewModelLifetime?.ViewModel;

        public UpdateViewModel SharedUpdateViewModel => ShellPage.ShellHandler.UpdateViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralPage"/> class.
        /// General Settings page constructor.
        /// </summary>
        public GeneralPage()
        {
            // Recreate the model before the generated Loading handler initializes x:Bind.
            Loading += GeneralPage_Loading;
            InitializeComponent();
            _viewModelLifetime = new PageViewModelLifetime<GeneralViewModel>(CreateViewModel);
            InitializeViewModel();
            Unloaded += GeneralPage_Unloaded;
            Loaded += GeneralPage_Loaded;
        }

        private GeneralViewModel CreateViewModel()
        {
            // Load string resources
            var loader = Helpers.ResourceLoaderInstance.ResourceLoader;
            var settingsUtils = SettingsUtils.Default;

            return new GeneralViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                loader.GetString("GeneralSettings_RunningAsAdminText"),
                loader.GetString("GeneralSettings_RunningAsUserText"),
                ShellPage.IsElevated,
                ShellPage.IsUserAnAdmin,
                ShellPage.SendDefaultIPCMessage,
                ShellPage.SendRestartAdminIPCMessage,
                ShellPage.ShellHandler.UpdateViewModel.CheckForUpdates,
                string.Empty,
                HideBackupAndRestoreMessageArea,
                RefreshBackupRestoreStatus,
                PickSingleFolderDialog,
                loader);
        }

        private void InitializeViewModel()
        {
            DataContext = ViewModel;
            ViewModel.InitializeReportBugLink();
            RegisterBugReportHandler();
            CheckBugReportStatus();
            RefreshBackupRestoreStatus(100);
        }

        private void GeneralPage_Loading(FrameworkElement sender, object args)
        {
            if (_viewModelLifetime.Load())
            {
                InitializeViewModel();
                Bindings.Update();
            }
        }

        private void GeneralPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.OnPageLoaded();
            if (SharedUpdateViewModel.CurrentUpdateUIState != UpdateViewModel.UpdateUIState.UpToDate)
            {
                SharedUpdateViewModel.RequestActivity();
            }
        }

        private void UpdateStatusCard_Click(object sender, RoutedEventArgs e)
        {
            ShellPage.ShellHandler?.OpenUpdateActivity();
        }

        private void OpenColorsSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.ColorsSettings);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while trying to open the system color settings", ex);
            }
        }

        private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
        {
            ((App)App.Current)!.OpenScoobe();
        }

        private void OpenDiagnosticsAndFeedbackSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.DiagnosticsAndFeedback);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while trying to open the system Diagnostics & Feedback settings", ex);
            }
        }

        private void RefreshBackupRestoreStatus(int delayMs = 0)
        {
            var viewModel = ViewModel;
            var cancellationToken = _viewModelLifetime.CancellationToken;
            var dispatcher = DispatcherQueue;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    SettingsBackupAndRestoreUtils.Instance.DryRunBackup();
                    dispatcher.TryEnqueue(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            viewModel.NotifyAllBackupAndRestoreProperties();
                        }
                    });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The page was unloaded while its initial/delayed refresh was pending.
                }
            });
        }

        private void HideBackupAndRestoreMessageArea()
        {
            var viewModel = ViewModel;
            var cancellationToken = _viewModelLifetime.CancellationToken;
            DispatcherQueue.TryEnqueue(async () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                const int messageShowTimeMs = 10000;
                OkToHideBackupAndRestoreMessageTime = DateTime.UtcNow.AddMilliseconds(messageShowTimeMs - 16);
                try
                {
                    await Task.Delay(messageShowTimeMs, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested && DateTime.UtcNow > OkToHideBackupAndRestoreMessageTime)
                    {
                        viewModel.HideBackupAndRestoreMessageArea();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // A message from the previous load must not update a replacement model.
                }
            });
        }

        private void UpdateBackupAndRestoreStatusText(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            RefreshBackupRestoreStatus();
        }

        private async Task<string> PickSingleFolderDialog()
        {
            // This function was changed to use the shell32 API to open folder dialog
            // as the old one (PickSingleFolderAsync) can't work when the process is elevated
            // TODO: go back PickSingleFolderAsync when it's fixed
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            string r = await Task.FromResult<string>(ShellGetFolder.GetFolderDialog(hwnd));
            return r;
        }

        private void Click_LanguageRestart(object sender, RoutedEventArgs e)
        {
            ViewModel.Restart();
        }

        private void Click_ViewDiagnosticDataViewerRestart(object sender, RoutedEventArgs e)
        {
            ViewModel.Restart();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshSettingsOnExternalChange();
        }

        private async void ViewDiagnosticData_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(ViewModel.ViewDiagnosticData);
        }

        private void BugReportToolClicked(object sender, RoutedEventArgs e)
        {
            // Start bug report
            ShellPage.SendDefaultIPCMessage("{\"bugreport\": 0 }");

            ViewModel.IsBugReportRunning = true;

            // No need to start timer - the observer pattern will notify us when it finishes
        }

        private void CheckBugReportStatus()
        {
            // Send one-time request to check current bug report status
            string ipcMessage = "{ \"bug_report_status\": { } }";
            ShellPage.SendDefaultIPCMessage(ipcMessage);
        }

        private void RegisterBugReportHandler()
        {
            var viewModel = ViewModel;
            var cancellationToken = _viewModelLifetime.CancellationToken;
            var dispatcher = DispatcherQueue;
            _bugReportStatusHandler = response =>
            {
                if (!cancellationToken.IsCancellationRequested && response.ContainsKey("bug_report_running"))
                {
                    var isRunning = response.GetNamedBoolean("bug_report_running");
                    dispatcher.TryEnqueue(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            viewModel.IsBugReportRunning = isRunning;
                        }
                    });
                }
            };
            ShellPage.ShellHandler.IPCResponseHandleList.Add(_bugReportStatusHandler);
        }

        private void GeneralPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
            CleanupBugReportHandlers();
            _viewModelLifetime.Unload();
        }

        public void Dispose()
        {
            Bindings.StopTracking();
            Loading -= GeneralPage_Loading;
            Loaded -= GeneralPage_Loaded;
            Unloaded -= GeneralPage_Unloaded;
            CleanupBugReportHandlers();
            _viewModelLifetime.Dispose();
            GC.SuppressFinalize(this);
        }

        private void CleanupBugReportHandlers()
        {
            // Remove IPC handler
            if (ShellPage.ShellHandler?.IPCResponseHandleList != null)
            {
                ShellPage.ShellHandler.IPCResponseHandleList.Remove(_bugReportStatusHandler);
            }

            _bugReportStatusHandler = null;
        }

        private void ShowSystemTrayIcon_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                var shellViewModel = ShellPage.ShellHandler?.ViewModel;
                if (shellViewModel != null)
                {
                    shellViewModel.ShowCloseMenu = !toggleSwitch.IsOn;
                }
            }
        }
    }
}
