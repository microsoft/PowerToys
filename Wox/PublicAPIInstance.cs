using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.ViewModel;

namespace Wox
{
    public class PublicAPIInstance : IPublicAPI
    {

        #region Constructor

        public PublicAPIInstance(MainViewModel mainVM)
        {
            MainVM = mainVM;


            GlobalHotkey.Instance.hookedKeyboardCallback += KListener_hookedKeyboardCallback;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());

            SetHotkey(UserSettingStorage.Instance.Hotkey, OnHotkey);
            SetCustomPluginHotkey();

            MainVM.ListeningKeyPressed += (o, e) => {

                if(e.KeyEventArgs.Key == Key.Back)
                {
                    if (null != BackKeyDownEvent)
                    {
                        BackKeyDownEvent(new WoxKeyDownEventArgs
                        {
                            Query = MainVM.QueryText,
                            keyEventArgs = e.KeyEventArgs
                        });
                    }
                }

            };
        }

        #endregion

        #region Properties

        private MainViewModel MainVM
        {
            get;
            set;
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            MainVM.QueryText = query;
            MainVM.CaretIndex = MainVM.QueryText.Length;

        }

        public void ChangeQueryText(string query, bool selectAll = false)
        {
            MainVM.QueryText = query;
            MainVM.SelectAllText = true;
        }

        public void CloseApp()
        {
            //notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        public void RestarApp()
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = Application.ResourceAssembly.Location,
                Arguments = SingleInstance<App>.Restart
            };
            Process.Start(info);
        }

        public void HideApp()
        {
            HideWox();
        }

        public void ShowApp()
        {
            ShowWox();
        }

        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var m = new Msg { Owner = Application.Current.MainWindow };
                m.Show(title, subTitle, iconPath);
            });
        }

        public void OpenSettingDialog(string tabName = "general")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>(this);
                sw.SwitchTo(tabName);
            });
        }

        public void StartLoadingBar()
        {
            MainVM.IsProgressBarVisible = true;
        }

        public void StopLoadingBar()
        {
            MainVM.IsProgressBarVisible = false;
        }

        public void InstallPlugin(string path)
        {
            Application.Current.Dispatcher.Invoke(() => PluginManager.InstallPlugin(path));
        }

        public void ReloadPlugins()
        {
            Application.Current.Dispatcher.Invoke(() => PluginManager.Init(this));
        }

        public string GetTranslation(string key)
        {
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins.ToList();
        }

        public event WoxKeyDownEventHandler BackKeyDownEvent;
        public event WoxGlobalKeyboardEventHandler GlobalKeyboardEvent;
        public event ResultItemDropEventHandler ResultItemDropEvent;

        public void PushResults(Query query, PluginMetadata plugin, List<Result> results)
        {
            results.ForEach(o =>
            {
                o.PluginDirectory = plugin.PluginDirectory;
                o.PluginID = plugin.ID;
                o.OriginQuery = query;
            });
            MainVM.UpdateResultView(results, plugin, query);
        }

        public void ShowContextMenu(PluginMetadata plugin, List<Result> results)
        {
            if (results != null && results.Count > 0)
            {
                results.ForEach(o =>
                {
                    o.PluginDirectory = plugin.PluginDirectory;
                    o.PluginID = plugin.ID;
                });

                MainVM.ShowContextMenu(results, plugin.ID);
            }
        }

        #endregion

        #region Private Methods

        private bool KListener_hookedKeyboardCallback(KeyEvent keyevent, int vkcode, SpecialKeyState state)
        {
            if (GlobalKeyboardEvent != null)
            {
                return GlobalKeyboardEvent((int)keyevent, vkcode, state);
            }
            return true;
        }

        private void HideWox()
        {
            UserSettingStorage.Instance.WindowLeft = MainVM.Left;
            UserSettingStorage.Instance.WindowTop = MainVM.Top;
            MainVM.IsVisible = false;
        }

        private void ShowWox(bool selectAll = true)
        {
            UserSettingStorage.Instance.IncreaseActivateTimes();
            MainVM.IsVisible = true;
            MainVM.SelectAllText = true;
        }

        public void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
        {
            var hotkey = new HotkeyModel(hotkeyStr);
            SetHotkey(hotkey, action);
        }

        public void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
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

        public void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        /// <summary>
        /// Checks if Wox should ignore any hotkeys
        /// </summary>
        /// <returns></returns>
        private bool ShouldIgnoreHotkeys()
        {
            //double if to omit calling win32 function
            if (UserSettingStorage.Instance.IgnoreHotkeysOnFullscreen)
                if (WindowIntelopHelper.IsWindowFullscreen())
                    return true;

            return false;
        }

        private void SetCustomPluginHotkey()
        {
            if (UserSettingStorage.Instance.CustomPluginHotkeys == null) return;
            foreach (CustomPluginHotkey hotkey in UserSettingStorage.Instance.CustomPluginHotkeys)
            {
                CustomPluginHotkey hotkey1 = hotkey;
                SetHotkey(hotkey.Hotkey, delegate
                {
                    if (ShouldIgnoreHotkeys()) return;
                    ShowApp();
                    ChangeQuery(hotkey1.ActionKeyword, true);
                });
            }
        }

        private void OnHotkey(object sender, HotkeyEventArgs e)
        {
            if (ShouldIgnoreHotkeys()) return;
            ToggleWox();
            e.Handled = true;
        }

        private void ToggleWox()
        {
            if (!MainVM.IsVisible)
            {
                ShowWox();
            }
            else
            {
                HideWox();
            }
        }

        #endregion
    }
}
