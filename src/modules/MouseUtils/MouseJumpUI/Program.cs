// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using MouseJumpUI.Helpers;
using MouseJumpUI.HotKeys;

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

        // make sure we're in the right high dpi mode otherwise pixel positions and sizes for
        // screen captures get distorted and coordinates aren't various calculated correctly.
        if (Application.HighDpiMode != HighDpiMode.PerMonitorV2)
        {
            Logger.LogError("High dpi mode is not set to PerMonitorV2.");
            return;
        }

        var previewForm = new MainForm();

        // touch the form handle - this will force the handle to be created if it hasn't
        // been already (we'll get an error from previewForm.BeginInvoke() if the form
        // handle doesn't exist). note that BeginInvoke() will block whatever thread is
        // the owner of the handle so we need to make sure it gets created on the main
        // application thread otherwise we might block something important like the the
        // hotkey message loop
        var previewHwnd = previewForm.Handle;

        ConfigHelper.SetAppSettingsPath(
                new SettingsUtils().GetSettingsFilePath(MouseJumpSettings.ModuleName));
        Logger.LogInfo($"app settings path = '{ConfigHelper.AppSettingsPath}'");
        ConfigHelper.SetHotKeyEventHandler(
            (_, _) =>
            {
                // invoke on the thread the form was created on. this avoids
                // blocking the calling thread (e.g. the message loop as a
                // result of hotkey activation)
                previewForm.BeginInvoke(
                    () =>
                    {
                        previewForm.ShowPreview();
                    });
            });

        // load the application settings and start the filesystem watcher
        // so we reload if it changes
        ConfigHelper.LoadAppSettings();
        ConfigHelper.StartWatcher();

        Application.Run();
    }
}
