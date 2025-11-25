using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input; // For ICommand if needed
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI
{
    public class OverlaySettings : INotifyPropertyChanged
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
                    var data = JsonSerializer.Deserialize<PowerToysSettingsWrapper>(json);

                    if (data?.Properties != null)
                    {
                        _currentConfig = data.Properties;
                        RefreshBrushes();
                    }
                }
            }
            catch (Exception) { /* Fail silently or log */ }
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
            catch { }
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

        public HotkeySettings SwitchMonitorHotkey => _currentConfig.SwitchMonitorHotkey;

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
                // Assuming color comes in as "#RRGGBB" string from C++ add_color_picker
                // If it comes as Int, change logic. Your C++ uses add_color_picker which usually saves string "#RRGGBB"
                if (!string.IsNullOrEmpty(colorStr))
                {
                    c = Microsoft.UI.Markup.XamlBindingHelper.ConvertValue(typeof(Color), colorStr) is Color ? (Color)Microsoft.UI.Markup.XamlBindingHelper.ConvertValue(typeof(Color), colorStr) : Colors.Black;
                }
            }
            catch {}

            byte alpha = (byte)(opacityPercent * 2.55);
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
    }

    // --- JSON Mapping Classes ---
    public class PowerToysSettingsWrapper
    {
        [JsonPropertyName("properties")]
        public SettingsRoot Properties { get; set; }
    }

    public class SettingsRoot
    {
        [JsonPropertyName("IsDraggableOverlayEnabled")]
        public BoolProperty IsDraggable { get; set; } = new BoolProperty { Value = true };

        [JsonPropertyName("OverlayTimeout")]
        public IntProperty OverlayTimeout { get; set; } = new IntProperty { Value = 3000 };

        [JsonPropertyName("TextSize")]
        public IntProperty TextSize { get; set; } = new IntProperty { Value = 24 };

        [JsonPropertyName("TextOpacity")]
        public IntProperty TextOpacity { get; set; } = new IntProperty { Value = 100 };

        [JsonPropertyName("BackgroundOpacity")]
        public IntProperty BackgroundOpacity { get; set; } = new IntProperty { Value = 50 };

        [JsonPropertyName("TextColor")]
        public StringProperty TextColor { get; set; } = new StringProperty { Value = "#FFFFFF" };

        [JsonPropertyName("BackgroundColor")]
        public StringProperty BackgroundColor { get; set; } = new StringProperty { Value = "#000000" };

        [JsonPropertyName("SwitchMonitorHotkey")]
        public HotkeySettings SwitchMonitorHotkey { get; set; } = new HotkeySettings { Win = true, Code = 75 /* K */ }; // Default Win+K
    }

    public class IntProperty { public int Value { get; set; } }

    public class BoolProperty { public bool Value { get; set; } }

    public class StringProperty { public string Value { get; set; } }

    public class HotkeySettings
    {
        [JsonPropertyName("win")]
        public bool Win { get; set; }

        [JsonPropertyName("ctrl")]
        public bool Ctrl { get; set; }

        [JsonPropertyName("alt")]
        public bool Alt { get; set; }

        [JsonPropertyName("shift")]
        public bool Shift { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }
}
