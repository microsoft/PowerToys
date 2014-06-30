using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox
{
    public partial class CustomPluginHotkeySetting : Window
    {
        private SettingWindow settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;

        public CustomPluginHotkeySetting(SettingWindow settingWidow)
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
                    MessageBox.Show("Hotkey is unavailable, please select a new hotkey");
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
                MessageBox.Show("Add hotkey successfully!");
            }
            else
            {
                if (updateCustomHotkey.Hotkey != ctlHotkey.CurrentHotkey.ToString() && !ctlHotkey.CurrentHotkeyAvailable)
                {
                    MessageBox.Show("Hotkey is unavailable, please select a new hotkey");
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
                MessageBox.Show("Update successfully!");
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
                MessageBox.Show("Invalid plugin hotkey");
                Close();
                return;
            }

            tbAction.Text = updateCustomHotkey.ActionKeyword;
            ctlHotkey.SetHotkey(updateCustomHotkey.Hotkey, false);
            update = true;
            lblAdd.Text = "Update";
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            settingWidow.MainWindow.ShowApp();
            settingWidow.MainWindow.ChangeQuery(tbAction.Text);
        }
    }
}
