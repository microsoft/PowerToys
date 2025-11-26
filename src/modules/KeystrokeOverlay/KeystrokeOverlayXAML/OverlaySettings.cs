// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using KeystrokeOverlayUI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI
{
    public partial class OverlaySettings : INotifyPropertyChanged, IDisposable
    {
        // Path to the PowerToys settings file
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft\\PowerToys\\Keystroke Overlay\\settings.json");

        private FileSystemWatcher _watcher;
        private SettingsRoot _currentConfig;

        public event PropertyChangedEventHandler PropertyChanged;

        public OverlaySettings()
        {
            // 1. Load initial defaults
            _currentConfig = new SettingsRoot();
            LoadSettings();

            // 2. Watch for changes (live updates from Settings Dashboard)
            SetupWatcher();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var data = JsonSerializer.Deserialize<SettingsWrapper>(json);

                    if (data?.Properties != null)
                    {
                        _currentConfig = data.Properties;
                        RefreshBrushes();
                    }
                }
            }
            catch (Exception)
            {
                /* Fail silently or log */
            }
        }

        private void SetupWatcher()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (Directory.Exists(dir))
                {
                    _watcher = new FileSystemWatcher(dir, "settings.json");
                    _watcher.Changed += (s, e) =>
                    {
                        // Slight delay to ensure file write is complete
                        System.Threading.Thread.Sleep(100);

                        // Dispatch to UI thread if necessary, or just reload data
                        // (Since we are just updating data properties, direct reload is usually fine,
                        // but PropertyChanged must be raised on UI thread if using advanced bindings)
                        LoadSettings();
                    };
                    _watcher.EnableRaisingEvents = true;
                }
            }
            catch
            {
            }
        }

        private void RefreshBrushes()
        {
            // Notify UI that all properties might have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayTimeout)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDraggable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBrush)));
        }

        // --- Properties bound in XAML ---
        public int TextSize => _currentConfig.TextSize.Value;

        public int OverlayTimeout => _currentConfig.OverlayTimeout.Value;

        public bool IsDraggable => _currentConfig.IsDraggable.Value;

        // Convert the "Int" color from JSON to a XAML Brush
        public SolidColorBrush BackgroundBrush => GetBrush(_currentConfig.BackgroundColor.Value, _currentConfig.BackgroundOpacity.Value);

        public SolidColorBrush TextBrush => GetBrush(_currentConfig.TextColor.Value, _currentConfig.TextOpacity.Value);

        // Helper to convert BGR/RGB int + Opacity to Brush
        private SolidColorBrush GetBrush(string colorStr, int opacityPercent)
        {
            // Default fallback
            Color c = Colors.Black;

            try
            {
                if (!string.IsNullOrEmpty(colorStr))
                {
                    // Parse "#RRGGBB"
                    colorStr = colorStr.Replace("#", string.Empty);
                    byte r = Convert.ToByte(colorStr.Substring(0, 2), 16);
                    byte g = Convert.ToByte(colorStr.Substring(2, 2), 16);
                    byte b = Convert.ToByte(colorStr.Substring(4, 2), 16);
                    c = Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
            }

            byte alpha = (byte)(opacityPercent * 2.55);
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }

        // IDisposable implementation for CA1001
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_watcher != null)
                {
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
        }
    }

    // public class HotkeySettings
    // {
    //     [JsonPropertyName("win")]
    //     public bool Win { get; set; }
    //     [JsonPropertyName("ctrl")]
    //     public bool Ctrl { get; set; }
    //     [JsonPropertyName("alt")]
    //     public bool Alt { get; set; }
    //     [JsonPropertyName("shift")]
    //     public bool Shift { get; set; }
    //     [JsonPropertyName("code")]
    //     public int Code { get; set; }
    // }
}
