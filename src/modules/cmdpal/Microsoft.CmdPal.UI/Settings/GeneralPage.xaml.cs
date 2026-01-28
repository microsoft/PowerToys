// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI.Settings;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - Page lifecycle manages disposal
public sealed partial class GeneralPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    private readonly ILogger _logger;
    private readonly SettingsViewModel viewModel;

    public GeneralPage(SettingsService settingsService, TopLevelCommandManager topLevelCommandManager, IThemeService themeService, ILogger logger)
    {
        this.InitializeComponent();

        _logger = logger;
        viewModel = new SettingsViewModel(settingsService, topLevelCommandManager, _mainTaskScheduler, themeService);
    }

    public string ApplicationVersion
    {
        get
        {
            var versionNo = ResourceLoaderInstance.GetString("Settings_GeneralPage_VersionNo");
            if (!TryGetPackagedVersion(out var version) && !TryGetAssemblyVersion(out version))
            {
                version = "?";
            }

            return string.Format(CultureInfo.CurrentCulture, versionNo, version);
        }
    }

    private bool TryGetPackagedVersion(out string version)
    {
        version = string.Empty;
        try
        {
            // Package.Current throws InvalidOperationException if the app is not packaged
            var v = Package.Current.Id.Version;
            version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log_ErrorGettingVersionFromPackage(ex);
            return false;
        }
    }

    private bool TryGetAssemblyVersion(out string version)
    {
        version = string.Empty;
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                return false;
            }

            var info = FileVersionInfo.GetVersionInfo(processPath);
            version = $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
            return true;
        }
        catch (Exception ex)
        {
            Log_ErrorGettingVersionFromExecutable(ex);
            return false;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to get version from Package.Current")]
    partial void Log_ErrorGettingVersionFromPackage(Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to get version from the executable")]
    partial void Log_ErrorGettingVersionFromExecutable(Exception ex);
}
