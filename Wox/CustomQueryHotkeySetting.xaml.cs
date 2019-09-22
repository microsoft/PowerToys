using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using NHotkey;
using NHotkey.Wpf;
using Wox.Core.Resource;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.UserSettings;

namespace Wox
{
    public partial class CustomQueryHotkeySetting : Window
    {
        private SettingWindow _settingWidow;
        private bool update;
        private CustomPluginHotkey updateCustomHotkey;
        private Settings _settings;

        public CustomQueryHotkeySetting(SettingWindow settingWidow, Settings settings)
        {
            _settingWidow = settingWidow;
            InitializeComponent();
            _settings = settings;
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

                if (_settings.CustomPluginHotkeys == null)
                {
                    _settings.CustomPluginHotkeys = new ObservableCollection<CustomPluginHotkey>();
                }

                var pluginHotkey = new CustomPluginHotkey
                {
                    Hotkey = ctlHotkey.CurrentHotkey.ToString(),
                    ActionKeyword = tbAction.Text
                };
                _settings.CustomPluginHotkeys.Add(pluginHotkey);

                SetHotkey(ctlHotkey.CurrentHotkey, delegate
                {
                    App.API.ChangeQuery(pluginHotkey.ActionKeyword);
                    Application.Current.MainWindow.Visibility = Visibility.Visible;
                });
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("success"));
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
                RemoveHotkey(oldHotkey);
                SetHotkey(new HotkeyModel(updateCustomHotkey.Hotkey), delegate
                {
                    App.API.ChangeQuery(updateCustomHotkey.ActionKeyword);
                    Application.Current.MainWindow.Visibility = Visibility.Visible;
                });
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("success"));
            }

            Close();
        }

        public void UpdateItem(CustomPluginHotkey item)
        {
            updateCustomHotkey = _settings.CustomPluginHotkeys.FirstOrDefault(o => o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
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
            App.API.ChangeQuery(tbAction.Text);
            Application.Current.MainWindow.Visibility = Visibility.Visible;
        }

        private void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        private void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg = string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }
    }
}
