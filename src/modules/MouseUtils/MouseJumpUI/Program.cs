// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace MouseJumpUI;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Logger.InitializeLogger("\\MouseJump\\Logs");

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseJumpEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
        {
            // TODO : Log message
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return;
        }

        if (Application.HighDpiMode != HighDpiMode.PerMonitorV2)
        {
            Logger.LogError("High dpi mode is not set to PerMonitorV2.");
            return;
        }

        var settings = Program.ReadSettings();
        var mainForm = new MainForm(settings);

        Application.Run(mainForm);
    }

    private static MouseJumpSettings ReadSettings()
    {
        var settingsUtils = new SettingsUtils();
        var settingsPath = settingsUtils.GetSettingsFilePath(MouseJumpSettings.ModuleName);
        if (!File.Exists(settingsPath))
        {
            var scaffoldSettings = new MouseJumpSettings();
            settingsUtils.SaveSettings(JsonSerializer.Serialize(scaffoldSettings), MouseJumpSettings.ModuleName);
        }

        var settings = new MouseJumpSettings();
        try
        {
            settings = settingsUtils.GetSettings<MouseJumpSettings>(MouseJumpSettings.ModuleName);
        }
        catch (Exception ex)
        {
            var errorMessage = $"There was a problem reading the configuration file. Error: {ex.GetType()} {ex.Message}";
            Logger.LogInfo(errorMessage);
            Logger.LogDebug(errorMessage);
        }

        return settings;
    }
}
