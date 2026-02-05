// SnapAssistDelaySettings.cs - Fix for Issue #36943
using System;
namespace FancyZones.Settings
{
    public class SnapAssistDelaySettings
    {
        public int DelayMilliseconds { get; set; } = 300;
        public bool EnableDelay { get; set; } = true;
        public int MinDelay { get; } = 0;
        public int MaxDelay { get; } = 2000;
    }
}
