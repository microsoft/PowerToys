using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.UserSettings;

namespace Wox
{
    public partial class CustomQueryHotkeySetting : Window
    {
        private SettingWindow settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;

        public CustomQueryHotkeySetting(SettingWindow settingWidow)
        {
            this.settingWidow = settingWidow;
            InitializeComponent();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            if (!update)
            {
                if (!ctlHotkey.CurrentHotkeyAvailable)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("hotkeyIsNotUnavailable"));
                    return;
                }

                if (UserSettingStorage.Instance.CustomPluginHotkeys == null)
                {
                    UserSettingStorage.Instance.CustomPluginHotkeys = new List<CustomPluginHotkey>();
                }

                var pluginHotkey = new CustomPluginHotkey()
                {
                    Hotkey = ctlHotkey.CurrentHotkey.ToString(),
                    ActionKeyword = tbAction.Text
                };
                UserSettingStorage.Instance.CustomPluginHotkeys.Add(pluginHotkey);
                settingWidow.MainWindow.SetHotkey(ctlHotkey.CurrentHotkey.ToString(), delegate
                {
                    settingWidow.MainWindow.ChangeQuery(pluginHotkey.ActionKeyword);
                    settingWidow.MainWindow.ShowApp();
                });
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
            }
            else
            {
                if (updateCustomHotkey.Hotkey != ctlHotkey.CurrentHotkey.ToString() && !ctlHotkey.CurrentHotkeyAvailable)
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("hotkeyIsNotUnavailable"));
                    return;
                }
                var oldHotkey = updateCustomHotkey.Hotkey;
                updateCustomHotkey.ActionKeyword = tbAction.Text;
                updateCustomHotkey.Hotkey = ctlHotkey.CurrentHotkey.ToString();
                //remove origin hotkey
                settingWidow.MainWindow.RemoveHotkey(oldHotkey);
                settingWidow.MainWindow.SetHotkey(updateCustomHotkey.Hotkey, delegate
                {
                    settingWidow.MainWindow.ShowApp();
                    settingWidow.MainWindow.ChangeQuery(updateCustomHotkey.ActionKeyword);
                });
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
            }

            UserSettingStorage.Instance.Save();
            settingWidow.ReloadCustomPluginHotkeyView();
            Close();
        }

        public void UpdateItem(CustomPluginHotkey item)
        {
            updateCustomHotkey = UserSettingStorage.Instance.CustomPluginHotkeys.FirstOrDefault(o => o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
            if (updateCustomHotkey == null)
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidPluginHotkey"));
                Close();
                return;
            }

            tbAction.Text = updateCustomHotkey.ActionKeyword;
            ctlHotkey.SetHotkey(updateCustomHotkey.Hotkey, false);
            update = true;
            lblAdd.Text = InternationalizationManager.Instance.GetTranslation("update");
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            settingWidow.MainWindow.ShowApp();
            settingWidow.MainWindow.ChangeQuery(tbAction.Text);
        }
    }
}
