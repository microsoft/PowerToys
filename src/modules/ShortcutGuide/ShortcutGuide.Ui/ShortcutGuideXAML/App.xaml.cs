// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using ShortcutGuide.Models;
using ShortcutGuide.ShortcutGuideXAML;

namespace ShortcutGuide
{
    public partial class App
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            LoadData();
            MainWindow = new MainWindow();
            TaskBarWindow = new TaskbarWindow();
            MainWindow.Activate();
            MainWindow.Closed += (_, _) =>
            {
                Current.Exit();
            };
        }

        private void LoadData()
        {
            SettingsUtils settingsUtils = new();

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                PinnedShortcuts = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath))!;
            }

            ShortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig;
            ShortcutGuideProperties = ShortcutGuideSettings.Properties;

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            settingsUtils.SaveSettings(JsonSerializer.Serialize(App.ShortcutGuideSettings, new JsonSerializerOptions { WriteIndented = true }), "Shortcut Guide");
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        }

        internal static Dictionary<string, List<ShortcutEntry>> PinnedShortcuts { get; private set; } = null!;

        internal static ShortcutGuideSettings ShortcutGuideSettings { get; private set; } = null!;

        internal static ShortcutGuideProperties ShortcutGuideProperties { get; private set; } = null!;

        internal static MainWindow MainWindow { get; private set; } = null!;

        internal static TaskbarWindow TaskBarWindow { get; private set; } = null!;
    }
}
