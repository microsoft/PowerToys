// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class GeneralPage : Page, INotifyPropertyChanged
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private readonly IApplicationInfoService _appInfoService;
    private readonly DispatcherTimer _notificationStateTimer;

    private bool _isNotificationStateSuppressing;
    private string _notificationStateMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GeneralPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        _appInfoService = App.Current.Services.GetRequiredService<IApplicationInfoService>();
        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);

        _notificationStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _notificationStateTimer.Tick += NotificationStateTimer_Tick;

        Loaded += GeneralPage_Loaded;
        Unloaded += GeneralPage_Unloaded;
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
        UpdateNotificationState();
        _notificationStateTimer.Start();
    }

    private void GeneralPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _notificationStateTimer.Stop();
    }

    private void NotificationStateTimer_Tick(object? sender, object e)
    {
        UpdateNotificationState();
    }

    private void UpdateNotificationState()
    {
        var state = WindowHelper.GetUserNotificationState();

        if (state is UserNotificationState.QUNS_RUNNING_D3D_FULL_SCREEN or
            UserNotificationState.QUNS_PRESENTATION_MODE or
            UserNotificationState.QUNS_BUSY)
        {
            var stateDescription = state switch
            {
                UserNotificationState.QUNS_RUNNING_D3D_FULL_SCREEN => ResourceLoaderInstance.GetString("NotificationState_D3DFullScreen"),
                UserNotificationState.QUNS_PRESENTATION_MODE => ResourceLoaderInstance.GetString("NotificationState_PresentationMode"),
                UserNotificationState.QUNS_BUSY => ResourceLoaderInstance.GetString("NotificationState_Busy"),
                _ => string.Empty,
            };

            var messageFormat = ResourceLoaderInstance.GetString("Settings_GeneralPage_NotificationState_InfoBar");
            NotificationStateMessage = string.Format(CultureInfo.CurrentCulture, messageFormat, stateDescription);
            IsNotificationStateSuppressing = true;
        }
        else
        {
            IsNotificationStateSuppressing = false;
        }
    }
}
