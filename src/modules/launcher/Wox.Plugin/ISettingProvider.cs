using Microsoft.PowerToys.Settings.UI.Lib;
using System.Windows.Controls;

namespace Wox.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();
        void UpdateSettings(PowerLauncherSettings settings);
    }
}
