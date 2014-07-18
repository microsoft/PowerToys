using System.Diagnostics;
using System.Drawing;

namespace Wox.Plugin.SystemPlugins.ControlPanel
{
    //from:https://raw.githubusercontent.com/CoenraadS/Windows-Control-Panel-Items
    public class ControlPanelItem
    {
        public string LocalizedString { get; private set; }
        public string InfoTip { get; private set; }
        public string ApplicationName { get; private set; }
        public ProcessStartInfo ExecutablePath { get; private set; }
        public Icon Icon { get; private set; }
        public int Score { get; set; }

        public ControlPanelItem(string newLocalizedString, string newInfoTip, string newApplicationName, ProcessStartInfo newExecutablePath, Icon newLargeIcon)
        {
            LocalizedString = newLocalizedString;
            InfoTip = newInfoTip;
            ApplicationName = newApplicationName;
            ExecutablePath = newExecutablePath;
            Icon = (Icon)newLargeIcon.Clone();
        }
    }
}
