// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.PowerToys.Settings.UI.Library;
using ManagedCommon;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// 管理应用程序主题设置
    /// </summary>
    public static class ThemeManager
    {
        private const string ThemeSettingKey = "AppTheme";
        private static readonly string SettingsFilePath;
        private static readonly ISettingsUtils _settingsUtils = new SettingsUtils();

        static ThemeManager()
        {
            // 使用本地AppData文件夹存储设置
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, "PowerDisplay");

            // 确保文件夹存在
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            SettingsFilePath = Path.Combine(appFolder, "theme.settings");
        }

        /// <summary>
        /// 获取保存的主题设置
        /// </summary>
        public static ElementTheme GetSavedTheme()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var savedTheme = File.ReadAllText(SettingsFilePath);
                    return savedTheme switch
                    {
                        "Light" => ElementTheme.Light,
                        "Dark" => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                }
            }
            catch
            {
                // 如果读取失败，返回默认值
            }

            return ElementTheme.Default;
        }

        /// <summary>
        /// 保存主题设置
        /// </summary>
        public static void SaveTheme(ElementTheme theme)
        {
            try
            {
                File.WriteAllText(SettingsFilePath, theme.ToString());
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 应用主题到窗口
        /// </summary>
        public static void ApplyTheme(Window window, ElementTheme theme)
        {
            if (window?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
                SaveTheme(theme);
            }
        }

        /// <summary>
        /// 切换主题（深色/浅色）
        /// </summary>
        public static ElementTheme ToggleTheme(Window window)
        {
            var currentTheme = GetCurrentTheme(window);
            var newTheme = currentTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;

            ApplyTheme(window, newTheme);
            return newTheme;
        }

        /// <summary>
        /// 获取当前窗口的主题
        /// </summary>
        public static ElementTheme GetCurrentTheme(Window window)
        {
            if (window?.Content is FrameworkElement rootElement)
            {
                return rootElement.RequestedTheme switch
                {
                    ElementTheme.Light => ElementTheme.Light,
                    ElementTheme.Dark => ElementTheme.Dark,
                    _ => Application.Current.RequestedTheme == ApplicationTheme.Light
                        ? ElementTheme.Light
                        : ElementTheme.Dark
                };
            }

            return ElementTheme.Default;
        }

        /// <summary>
        /// 判断是否为深色主题
        /// </summary>
        public static bool IsDarkTheme(Window window)
        {
            return GetCurrentTheme(window) == ElementTheme.Dark;
        }

        /// <summary>
        /// 从PowerToys设置中获取主题
        /// </summary>
        public static ElementTheme GetThemeFromPowerToysSettings()
        {
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");
                return settings.Properties.Theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
            catch
            {
                return ElementTheme.Default;
            }
        }

        /// <summary>
        /// 将主题保存到PowerToys设置
        /// </summary>
        public static void SaveThemeToPowerToysSettings(ElementTheme theme)
        {
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");
                settings.Properties.Theme = theme.ToString();
                _settingsUtils.SaveSettings(settings.ToJsonString(), "PowerDisplay");
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止操作
                try
                {
                    Logger.LogError($"Failed to save theme to PowerToys settings: {ex.Message}");
                }
                catch
                {
                    // 忽略日志错误
                }
            }
        }

        /// <summary>
        /// 获取保存的主题设置（优先从PowerToys设置读取）
        /// </summary>
        public static ElementTheme GetSavedThemeWithPriority()
        {
            // 首先尝试从PowerToys设置读取
            var powerToysTheme = GetThemeFromPowerToysSettings();
            if (powerToysTheme != ElementTheme.Default)
            {
                // 同步到本地设置
                SaveTheme(powerToysTheme);
                return powerToysTheme;
            }

            // 如果PowerToys设置没有或失败，回退到本地设置
            return GetSavedTheme();
        }

        /// <summary>
        /// 应用主题并同步到两个设置系统
        /// </summary>
        public static void ApplyThemeAndSync(Window window, ElementTheme theme)
        {
            // 应用到窗口
            ApplyTheme(window, theme);

            // 同步到PowerToys设置
            SaveThemeToPowerToysSettings(theme);
        }
    }
}
