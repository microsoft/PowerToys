using Wox.Plugin;

namespace Wox.Infrastructure.UserSettings
{
    public class CustomPluginHotkey : BaseModel
    {
        public string Hotkey { get; set; }
        public string ActionKeyword { get; set; }
    }
}
