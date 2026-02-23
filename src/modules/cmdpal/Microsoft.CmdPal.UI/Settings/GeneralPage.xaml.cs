// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class GeneralPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;
    private readonly IApplicationInfoService _appInfoService;

    public GeneralPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        _appInfoService = App.Current.Services.GetRequiredService<IApplicationInfoService>();
        var languageService = App.Current.Services.GetRequiredService<ILanguageService>();

        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService, languageService);
        viewModel.RestartRequested += OnRestartRequested;
    }

    private async void OnRestartRequested()
    {
        var dialog = new ContentDialog
        {
            Title = RS_.GetString("Settings_GeneralPage_LanguageRestartDialog_Title"),
            PrimaryButtonText = RS_.GetString("Settings_GeneralPage_LanguageRestartDialog_RestartNowButtonText"),
            CloseButtonText = RS_.GetString("Settings_GeneralPage_LanguageRestartDialog_LaterNowButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            RestartApp();
        }
    }

    private void LanguageRestartButton_Click(object sender, RoutedEventArgs e)
    {
        RestartApp();
    }

    private static void RestartApp()
    {
        try
        {
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        }
        catch
        {
            Application.Current.Exit();
        }
    }

    public string ApplicationVersion
    {
        get
        {
            var versionNo = RS_.GetString("Settings_GeneralPage_VersionNo");
            var version = _appInfoService.AppVersion;
            return string.Format(CultureInfo.CurrentCulture, versionNo, version);
        }
    }
}
