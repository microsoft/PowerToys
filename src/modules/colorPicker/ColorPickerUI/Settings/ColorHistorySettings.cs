// ColorHistorySettings.cs - Fix for Issue #32276
using System.Collections.Generic;
namespace ColorPicker.Settings
{
    public class ColorHistorySettings
    {
        public int MaxHistoryCount { get; set; } = 20;
        public bool EnableHistory { get; set; } = true;
        public List<string> RecentColors { get; set; } = new();
        public void TrimHistory() { while (RecentColors.Count > MaxHistoryCount) RecentColors.RemoveAt(0); }
    }
}
