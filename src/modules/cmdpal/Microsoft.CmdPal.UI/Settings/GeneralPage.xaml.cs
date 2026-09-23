// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class GeneralPage : Page, INotifyPropertyChanged
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private readonly IApplicationInfoService _appInfoService;
    private readonly ISettingsService _settingsService;
    private readonly IExternalCommandPermissionStore _externalCommandPermissionStore;
    private readonly DispatcherTimer _notificationStateTimer;

    private bool _hasExternalCommandPermissions;
    private bool _isPageLoaded;
    private bool _isNotificationStateSuppressing;
    private string _notificationStateMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ExternalCommandPermissionViewModel> ExternalCommandPermissions { get; } = [];

    public GeneralPage()
    {
        this.InitializeComponent();

        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        _settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        _externalCommandPermissionStore = App.Current.Services.GetRequiredService<IExternalCommandPermissionStore>();
        _appInfoService = App.Current.Services.GetRequiredService<IApplicationInfoService>();
        viewModel = new SettingsViewModel(topLevelCommandManager, _mainTaskScheduler, themeService, _settingsService);

        _notificationStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _notificationStateTimer.Tick += NotificationStateTimer_Tick;

        Loaded += GeneralPage_Loaded;
        Unloaded += GeneralPage_Unloaded;
    }

    public bool HasExternalCommandPermissions
    {
        get => _hasExternalCommandPermissions;
        private set
        {
            if (_hasExternalCommandPermissions != value)
            {
                _hasExternalCommandPermissions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasExternalCommandPermissions)));
            }
        }
    }

    public bool IsNotificationStateSuppressing
    {
        get => _isNotificationStateSuppressing;
        private set
        {
            if (_isNotificationStateSuppressing != value)
            {
                _isNotificationStateSuppressing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNotificationStateSuppressing)));
            }
        }
    }

    public string NotificationStateMessage
    {
        get => _notificationStateMessage;
        private set
        {
            if (_notificationStateMessage != value)
            {
                _notificationStateMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotificationStateMessage)));
            }
        }
    }

    public string ApplicationVersion
    {
        get
        {
            var versionNo = ResourceLoaderInstance.GetString("Settings_GeneralPage_VersionNo");
            var version = _appInfoService.AppVersion;
            return string.Format(CultureInfo.CurrentCulture, versionNo, version);
        }
    }

    private void GeneralPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        _externalCommandPermissionStore.PermissionsChanged += ExternalCommandPermissionStore_PermissionsChanged;
        UpdateNotificationState();
        _notificationStateTimer.Start();
        _ = RefreshExternalCommandPermissionsAsync();
    }

    private void GeneralPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        _notificationStateTimer.Stop();
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        _externalCommandPermissionStore.PermissionsChanged -= ExternalCommandPermissionStore_PermissionsChanged;
    }

    private void ExternalCommandPermissionStore_PermissionsChanged(object? sender, EventArgs e) =>
        _ = RefreshExternalCommandPermissionsAsync();

    private async Task RefreshExternalCommandPermissionsAsync()
    {
        try
        {
            var permissions = await _externalCommandPermissionStore.GetAllAsync();
            await DispatcherQueue.EnqueueAsync(() =>
            {
                if (_isPageLoaded)
                {
                    ApplyExternalCommandPermissions(permissions);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to refresh external command link permissions.", ex);
        }
    }

    private void ApplyExternalCommandPermissions(IReadOnlyList<ExternalCommandPermission> permissions)
    {
        ExternalCommandPermissions.Clear();
        foreach (var permission in permissions)
        {
            ExternalCommandPermissions.Add(new ExternalCommandPermissionViewModel(permission));
        }

        HasExternalCommandPermissions = ExternalCommandPermissions.Count > 0;
    }

    private async void RevokeExternalCommandPermission_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ExternalCommandPermissionViewModel viewModel })
            {
                await _externalCommandPermissionStore.RevokeAsync(viewModel.Permission.Key);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to revoke an external command link permission.", ex);
        }
    }

    private async void ClearExternalCommandPermissions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = ResourceLoaderInstance.GetString("Settings_GeneralPage_ExternalCommandPermissions_ClearConfirmation_Title"),
                Content = ResourceLoaderInstance.GetString("Settings_GeneralPage_ExternalCommandPermissions_ClearConfirmation_Description"),
                PrimaryButtonText = ResourceLoaderInstance.GetString("Settings_GeneralPage_ExternalCommandPermissions_ClearConfirmation_RemoveButton"),
                CloseButtonText = ResourceLoaderInstance.GetString("ConfirmationDialog_CancelButtonText"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            await _externalCommandPermissionStore.ClearAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clear external command link permissions.", ex);
        }
    }

    private void NotificationStateTimer_Tick(object? sender, object e)
    {
        UpdateNotificationState();
    }

    private void SettingsService_SettingsChanged(ISettingsService sender, SettingsModel settings)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateNotificationState();
            return;
        }

        DispatcherQueue.TryEnqueue(UpdateNotificationState);
    }

    private void UpdateNotificationState()
    {
        var state = WindowHelper.GetUserNotificationState();
        var notificationFlags = WindowHelper.GetUserNotificationFlags(state);

        if (IsActivationShortcutSuppressed(
            notificationFlags.IsFullscreenState,
            notificationFlags.IsBusy,
            viewModel?.IgnoreShortcutWhenFullscreen == true,
            viewModel?.IgnoreShortcutWhenBusy == true))
        {
            var stateDescription = state switch
            {
                QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN => ResourceLoaderInstance.GetString("NotificationState_D3DFullScreen"),
                QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE => ResourceLoaderInstance.GetString("NotificationState_PresentationMode"),
                QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY => ResourceLoaderInstance.GetString("NotificationState_Busy"),
                _ => string.Empty,
            };

            var messageFormat = ResourceLoaderInstance.GetString("Settings_GeneralPage_NotificationState_InfoBar");
            var message = string.Format(CultureInfo.CurrentCulture, messageFormat, stateDescription);

            if (state is QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY)
            {
                var triggerApps = WindowHelper.FindVisibleTriggerApps();
                if (triggerApps.Count > 0)
                {
                    var triggerFormat = ResourceLoaderInstance.GetString("NotificationState_TriggerApps");
                    message += " " + string.Format(CultureInfo.CurrentCulture, triggerFormat, string.Join(", ", triggerApps));
                }
            }

            NotificationStateMessage = message;
            IsNotificationStateSuppressing = true;
        }
        else
        {
            NotificationStateMessage = string.Empty;
            IsNotificationStateSuppressing = false;
        }
    }

    private static bool IsActivationShortcutSuppressed(
        bool isFullscreenState,
        bool isBusyState,
        bool ignoreShortcutWhenFullscreen,
        bool ignoreShortcutWhenBusy)
    {
        return (ignoreShortcutWhenFullscreen && isFullscreenState) ||
               (ignoreShortcutWhenBusy && isBusyState);
    }
}
