// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapperProjection;
using RunnerV2;
using RunnerV2.Helpers;
using RunnerV2.Models;
using Settings.UI.Library;

internal sealed class Program
{
    private static readonly SettingsUtils _settingsUtils = new();
    private static GeneralSettings _generalSettings = _settingsUtils.GetSettings<GeneralSettings>();

    public static GeneralSettings GeneralSettings => _generalSettings;

    private static void Main(string[] args)
    {
        switch (ShouldRunInSpecialMode(args))
        {
            case SpecialMode.None:
                break;
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
        bool runElevatedSetting = _generalSettings.RunElevated;
        bool hasRestartedElevatedArgment = args.Contains("--restartedElevated");

        Action afterInitializationAction = () => { };
        Version version = Assembly.GetExecutingAssembly().GetName().Version!;

        if ($"v{version.Major}.{version.Minor}.{version.Build}" != _settingsUtils.GetSettings<LastVersionRunSettings>(fileName: "last_version_run.json").LastVersion && (!_generalSettings.ShowWhatsNewAfterUpdates || GPOWrapper.GetDisableShowWhatsNewAfterUpdatesValue() != GpoRuleConfigured.Disabled))
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
                _ = Runner.Run(afterInitializationAction);

                // Todo: Save settings
                break;
            default:
                // Todo: scheudle restart as elevated
                throw new NotImplementedException();
        }
    }

    private static SpecialMode ShouldRunInSpecialMode(string[] args)
    {
        // TODO
        return SpecialMode.None;
    }
}
