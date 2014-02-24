using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;

namespace Wox
{
    public partial class CustomPluginHotkeySetting : Window
    {
        private SettingWidow settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;


        public CustomPluginHotkeySetting(SettingWidow settingWidow)
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

                if (CommonStorage.Instance.UserSetting.CustomPluginHotkeys == null)
                {
                    CommonStorage.Instance.UserSetting.CustomPluginHotkeys = new List<CustomPluginHotkey>();
                }

                var pluginHotkey = new CustomPluginHotkey()
                {
                    Hotkey = ctlHotkey.CurrentHotkey.ToString(),
                    ActionKeyword = tbAction.Text
                };
                CommonStorage.Instance.UserSetting.CustomPluginHotkeys.Add(pluginHotkey);
                settingWidow.MainWindow.SetHotkey(ctlHotkey.CurrentHotkey.ToString(), delegate
                {
                    settingWidow.MainWindow.ShowApp();
                    settingWidow.MainWindow.ChangeQuery(pluginHotkey.ActionKeyword);
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

            CommonStorage.Instance.Save();
            settingWidow.ReloadCustomPluginHotkeyView();
            Close();
        }

        public void UpdateItem(CustomPluginHotkey item)
        {
            updateCustomHotkey = CommonStorage.Instance.UserSetting.CustomPluginHotkeys.FirstOrDefault(o => o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
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
