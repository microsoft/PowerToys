// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class GeneralPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;

    public GeneralPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);
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

    private static bool TryGetPackagedVersion(out string version)
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
            Logger.LogError("Failed to get version from the package", ex);
            return false;
        }
    }

    private static bool TryGetAssemblyVersion(out string version)
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
            Logger.LogError("Failed to get version from the executable", ex);
            return false;
        }
    }
}
