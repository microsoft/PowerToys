using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI
{
    public partial class MainViewModel : ObservableObject
    {
        // Changed from string to KeyVisualItem to support individual properties
        public ObservableCollection<KeyVisualItem> PressedKeys { get; } = new();

        private readonly int _textSize = 100;

        public SolidColorBrush TextBrush => new SolidColorBrush(TextColorWithAlpha);
        public SolidColorBrush BackgroundBrush => new SolidColorBrush(BackgroundColorWithAlpha);

        private readonly string _textColor = "#FFFFFF";
        private readonly int _textOpacity = 100;
        private readonly string _backgroundColor = "#000000";
        private readonly int _backgroundOpacity = 50;

        public Color TextColorWithAlpha => GetColorWithAlpha(_textColor, _textOpacity);
        public Color BackgroundColorWithAlpha => GetColorWithAlpha(_backgroundColor, _backgroundOpacity);

        private Color GetColorWithAlpha(string hex, int opacity)
        {
            Color color = ParseColor(hex);
            byte alpha = (byte)(opacity * 2.55);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Colors.Black;
            var span = hex.AsSpan();
            if (span[0] == '#') span = span[1..];
            if (span.Length != 6) return Colors.Black;
            try
            {
                byte r = byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte g = byte.Parse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte b = byte.Parse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return Color.FromArgb(255, r, g, b);
            }
            catch { return Colors.Black; }
        }

        // ---------------------------
        // New API for adding a key
        // ---------------------------
        public void RegisterKey(string key, int durationMs = 2000, int textSize = -1)
        {
            if (textSize == -1)
            {
                textSize = _textSize;
            }

            var newItem = new KeyVisualItem { Text = key, Opacity = 1.0, TextSize = textSize};
            PressedKeys.Add(newItem);

            UpdateOpacities();

            if (PressedKeys.Count > 5)
            {
                PressedKeys.RemoveAt(0);
                UpdateOpacities();
            }

            // Pass the duration to the removal logic
            _ = RemoveKeyAfterDelayAsync(newItem, durationMs);
        }

        // 2. Add a helper to clear keys immediately (for switching phases)
        public void ClearKeys()
        {
            PressedKeys.Clear();
        }

        private void UpdateOpacities()
        {
            // Iterate through all keys
            for (int i = 0; i < PressedKeys.Count; i++)
            {
                var item = PressedKeys[i];

                // If the item is currently running its "death animation", skip it
                if (item.IsExiting) continue;

                // Calculate index from the end (Newest = 0)
                int indexFromEnd = PressedKeys.Count - 1 - i;

                // Decrease 10% for every step back
                double targetOpacity = 1.0 - (0.10 * indexFromEnd);

                // Clamp to valid range (e.g. don't go below 0.1 visible)
                item.Opacity = Math.Max(0.1, targetOpacity);
            }
        }

        private async Task RemoveKeyAfterDelayAsync(KeyVisualItem item, int durationMs)
        {
            // Wait the 2 seconds life time
            await Task.Delay(durationMs);

            // Mark as exiting so UpdateOpacities doesn't fight us
            item.IsExiting = true;

            //// Trigger the fade out (XAML ScalarTransition handles the animation)
            //item.Opacity = 0;

            //// Wait for the transition to finish (approx 300ms)
            //await Task.Delay(300);

            if (PressedKeys.Contains(item))
            {
                PressedKeys.Remove(item);
                // Re-adjust remaining keys
                UpdateOpacities();
            }
        }
    }

    // Wrapper class for the keys
    public partial class KeyVisualItem : ObservableObject
    {
        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private int _textSize;

        [ObservableProperty]
        private double _opacity = 1.0;

        public bool IsExiting { get; set; } = false;
    }
}
