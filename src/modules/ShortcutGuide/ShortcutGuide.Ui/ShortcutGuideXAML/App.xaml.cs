// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using ShortcutGuide.Models;
using ShortcutGuide.ShortcutGuideXAML;
using ShortcutGuide.Telemetry;

namespace ShortcutGuide
{
    public partial class App
    {
        internal static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts { get; private set; } = new Dictionary<string, List<ShortcutEntry>>();

        internal static ShortcutGuideSettings ShortcutGuideSettings { get; private set; } = null!;

        internal static ShortcutGuideProperties ShortcutGuideProperties { get; private set; } = null!;

        internal static MainWindow MainWindow { get; private set; } = null!;

        internal static TaskbarWindow TaskBarWindow { get; private set; } = null!;

        internal static string CurrentAppName { get; set; } = string.Empty;

        public App()
        {
            this.InitializeComponent();

            // Register process-wide exception handlers so a stray exception (e.g. an IO failure
            // during a fire-and-forget UI handler, or a background Task fault) gets logged
            // instead of taking the overlay down with an unhandled access violation in coreclr.
            // Without these the runtime tears the process down before our local catches can run.
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                this.LoadData();
                MainWindow = new MainWindow();
                TaskBarWindow = new TaskbarWindow();
                MainWindow.Activate();
                MainWindow.Closed += (_, _) =>
                {
                    PowerToysTelemetry.Log.WriteEvent(new ShortcutGuideSessionEvent(
                        MainWindow.SessionDurationMs,
                        MainWindow.CloseType));
                    TaskBarWindow.Close();
                };
            }
            catch (Exception ex)
            {
                // Any failure in launch is fatal for this short-lived overlay; log and exit
                // cleanly rather than letting WinUI surface a generic crash dialog.
                Logger.LogError("Failed to launch Shortcut Guide.", ex);
                Environment.Exit(1);
            }
        }

        private void LoadData()
        {
            SettingsUtils settingsUtils = SettingsUtils.Default;

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                try
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath));
                    if (loaded != null)
                    {
                        PinnedShortcuts = loaded;
                    }
                }
                catch (Exception ex) when (ex is JsonException
                                        or IOException
                                        or UnauthorizedAccessException)
                {
                    // Fall back to the empty default if the file is corrupt or unreadable.
                    Logger.LogWarning($"Failed to load pinned shortcuts from '{pinnedPath}'. Falling back to empty list. Reason: {ex.Message}");
                }
            }

            ShortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig;
            ShortcutGuideProperties = ShortcutGuideSettings.Properties;

            try
            {
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
                settingsUtils.SaveSettings(JsonSerializer.Serialize(App.ShortcutGuideSettings, new JsonSerializerOptions { WriteIndented = true }), "Shortcut Guide");
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Persisting the round-tripped settings is best-effort; the in-memory copy is still valid.
                Logger.LogWarning($"Failed to persist Shortcut Guide settings on launch. Reason: {ex.Message}");
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Exceptions raised on the UI thread land here. Mark handled so the runtime
            // does not terminate the process; the overlay can usually continue.
            Logger.LogError("Unhandled UI exception in Shortcut Guide.", e.Exception);
            e.Handled = true;
        }

        private static void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            // Background-thread exceptions reach here as a last resort; we cannot prevent
            // termination when IsTerminating is true, but at least we leave a log trail.
            if (e.ExceptionObject is Exception ex)
            {
                Logger.LogError($"Unhandled background exception in Shortcut Guide (IsTerminating={e.IsTerminating}).", ex);
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.LogError("Unobserved Task exception in Shortcut Guide.", e.Exception);
            e.SetObserved();
        }
    }
}
