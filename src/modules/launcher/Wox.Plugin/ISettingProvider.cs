using System.Windows.Controls;

namespace Wox.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();
    }
}
