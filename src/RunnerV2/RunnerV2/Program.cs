// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapperProjection;
using RunnerV2;
using RunnerV2.Helpers;
using RunnerV2.Models;
using Settings.UI.Library;

internal sealed class Program
{
    private static readonly SettingsUtils _settingsUtils = new();

    internal static GeneralSettings GeneralSettings => _settingsUtils.GetSettings<GeneralSettings>();

    private static void Main(string[] args)
    {
        Logger.InitializeLogger("\\RunnerLogs");

        string securityDescriptor =
        "O:BA" // Owner: Builtin (local) administrator
        + "G:BA" // Group: Builtin (local) administrator
        + "D:"
        + "(A;;0x7;;;PS)" // Access allowed on COM_RIGHTS_EXECUTE, _LOCAL, & _REMOTE for Personal self
        + "(A;;0x7;;;IU)" // Access allowed on COM_RIGHTS_EXECUTE for Interactive Users
        + "(A;;0x3;;;SY)" // Access allowed on COM_RIGHTS_EXECUTE, & _LOCAL for Local system
        + "(A;;0x7;;;BA)" // Access allowed on COM_RIGHTS_EXECUTE, _LOCAL, & _REMOTE for Builtin (local) administrator
        + "(A;;0x3;;;S-1-15-3-1310292540-1029022339-4008023048-2190398717-53961996-4257829345-603366646)" // Access allowed on COM_RIGHTS_EXECUTE, & _LOCAL for Win32WebViewHost package capability
        + "S:"
        + "(ML;;NX;;;LW)"; // Integrity label on No execute up for Low mandatory level

        COMUtils.InitializeCOMSecurity(securityDescriptor);

        switch (ShouldRunInSpecialMode(args))
        {
            case SpecialMode.None:
                break;
            case SpecialMode.UpdateNow:
                UpdateNow();
                return;
            default:
                throw new NotImplementedException("Special modes are not implemented yet.");
        }

        bool shouldOpenSettings = args.Any(s => s.StartsWith("--open-settings", StringComparison.InvariantCulture));
        bool shouldOpenSettingsToSpecificPage = args.Any(s => s.StartsWith("--open-settings=", StringComparison.InvariantCulture));

        // Check if PowerToys is already running
        if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
        {
            throw new NotImplementedException("Opening another instance window is not supported yet.");
        }

        /*
         * Todo: Data diagnotics
        */

        bool isElevated = ElevationHelper.IsProcessElevated();
        bool hasDontElevateArgument = args.Contains("--dont-elevate");
        bool runElevatedSetting = GeneralSettings.RunElevated;
        bool hasRestartedElevatedArgment = args.Contains("--restartedElevated");

        Action afterInitializationAction = () => { };
        Version version = Assembly.GetExecutingAssembly().GetName().Version!;

        if ($"v{version.Major}.{version.Minor}.{version.Build}" != _settingsUtils.GetSettings<LastVersionRunSettings>(fileName: "last_version_run.json").LastVersion && (!GeneralSettings.ShowWhatsNewAfterUpdates || GPOWrapper.GetDisableShowWhatsNewAfterUpdatesValue() != GpoRuleConfigured.Disabled))
        {
            afterInitializationAction += () =>
            {
                SettingsHelper.OpenSettingsWindow(showScoobeWindow: true);
            };
        }

        if (!_settingsUtils.GetSettings<OOBESettings>(fileName: "oobe_settings.json").OpenedAtFirstLaunch)
        {
            afterInitializationAction += () =>
            {
                SettingsHelper.OpenSettingsWindow(showOobeWindow: true);
            };
        }

        if (shouldOpenSettings)
        {
            afterInitializationAction += () =>
            {
                SettingsHelper.OpenSettingsWindow(additionalArguments: shouldOpenSettingsToSpecificPage ? args.First(s => s.StartsWith("--open-settings=", StringComparison.InvariantCulture)).Replace("--open-settings=", string.Empty, StringComparison.InvariantCulture) : null);
            };
        }

        // Set last version run
        _settingsUtils.SaveSettings(new LastVersionRunSettings() { LastVersion = $"v{version.Major}.{version.Minor}.{version.Build}" }.ToJsonString(), fileName: "last_version_run.json");

        switch ((isElevated, hasDontElevateArgument, runElevatedSetting, hasRestartedElevatedArgment))
        {
            case (true, true, false, _):
                // Todo: Scheudle restart as non elevated
                throw new NotImplementedException();
            case (true, _, _, _):
            case (_, _, false, _):
            case (_, true, _, _):
            case (false, _, _, true):
                GeneralSettings tempGeneralSettings = GeneralSettings;
                tempGeneralSettings.IsElevated = isElevated;
                _settingsUtils.SaveSettings(tempGeneralSettings.ToJsonString());

                Runner.Run(afterInitializationAction);
                break;
            default:
                ElevationHelper.RestartScheduled = ElevationHelper.RestartScheduledMode.RestartElevated;
                break;
        }

        ElevationHelper.RestartIfScheudled();
    }

    /// <summary>
    /// Returns whether the application should run in a special mode based on the provided arguments.
    /// </summary>
    /// <param name="args">The arguments passed to <see cref="Main(string[])"/></param>
    /// <returns>The <see cref="SpecialMode"/> the app should run in.</returns>
    private static SpecialMode ShouldRunInSpecialMode(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("powertoys://", StringComparison.InvariantCultureIgnoreCase))
        {
            Uri uri = new(args[0]);
            string host = uri.Host.ToLowerInvariant();
            return host switch
            {
                "update_now" => SpecialMode.UpdateNow,
                _ => SpecialMode.None,
            };
        }

        return SpecialMode.None;
    }

    /// <summary>
    /// Starts the update process for PowerToys.
    /// </summary>
    private static void UpdateNow()
    {
        Process.Start(new ProcessStartInfo()
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            FileName = "PowerToys.Update.exe",
            Arguments = "-update_now",
        });
    }
}
