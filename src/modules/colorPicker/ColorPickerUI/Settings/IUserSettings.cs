using Microsoft.PowerToys.Settings.UI.Lib;

namespace ColorPicker.Settings
{
    public interface IUserSettings
    {
        SettingItem<string> ActivationShortcut { get; }

        SettingItem<bool> ChangeCursor { get; }

        SettingItem<ColorRepresentationType> CopiedColorRepresentation { get; set; }
    }
}
