using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using NHotkey;
using NHotkey.Wpf;
using Squirrel;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Helper;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.ViewModel;
using static Wox.ViewModel.SettingWindowViewModel;

namespace Wox
{
    public class PublicAPIInstance : IPublicAPI
    {
        private readonly SettingWindowViewModel _settingsViewModel;

        #region Constructor

        public PublicAPIInstance(SettingWindowViewModel settingsViewModel, MainViewModel mainVM)
        {
            _settingsViewModel = settingsViewModel;
            MainVM = mainVM;
            //_settings = settings;
            GlobalHotkey.Instance.hookedKeyboardCallback += KListener_hookedKeyboardCallback;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());

        }

        #endregion

        #region Properties

        public MainViewModel MainVM
        {
            get;
            set;
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            MainVM.QueryText = query;
            MainVM.OnCursorMovedToEnd();
        }

        public void ChangeQueryText(string query, bool selectAll = false)
        {
            MainVM.QueryText = query;
            MainVM.OnTextBoxSelected();
        }

        public void CloseApp()
        {
            //notifyIcon.Visible = false;
            Application.Current.MainWindow.Close();
        }

        public void RestarApp()
        {
            HideWox();
            // we must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            var vm = (MainViewModel) Application.Current.MainWindow.DataContext;
            vm.Save();
            UpdateManager.RestartApp();
        }

        public void HideApp()
        {
            HideWox();
        }

        public void ShowApp()
        {
            ShowWox();
        }

        public void ShowMsg(string title, string subTitle = "", string iconPath = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var m = new Msg { Owner = Application.Current.MainWindow };
                m.Show(title, subTitle, iconPath);
            });
        }

        public void OpenSettingDialog(int tab = 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _settingsViewModel.SelectedTab = (Tab)tab;
                SettingWindow sw = SingletonWindowOpener.Open<SettingWindow>(this, _settingsViewModel);
            });
        }

        public void StartLoadingBar()
        {
            MainVM.ProgressBarVisibility = Visibility.Visible;
        }

        public void StopLoadingBar()
        {
            MainVM.ProgressBarVisibility = Visibility.Collapsed;
        }

        public void InstallPlugin(string path)
        {
            Application.Current.Dispatcher.Invoke(() => PluginManager.InstallPlugin(path));
        }

        public string GetTranslation(string key)
        {
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public List<PluginPair> GetAllPlugins()
        {
            return PluginManager.AllPlugins.ToList();
        }

        public event WoxGlobalKeyboardEventHandler GlobalKeyboardEvent;

        [Obsolete("This will be removed in Wox 1.3")]
        public void PushResults(Query query, PluginMetadata plugin, List<Result> results)
        {
            results.ForEach(o =>
            {
                o.PluginDirectory = plugin.PluginDirectory;
                o.PluginID = plugin.ID;
                o.OriginQuery = query;
            });
            Task.Run(() =>
            {
                MainVM.UpdateResultView(results, plugin, query);
            });
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
            MainVM.MainWindowVisibility = Visibility.Collapsed;
        }

        private void ShowWox(bool selectAll = true)
        {
            MainVM.MainWindowVisibility = Visibility.Visible;
            MainVM.OnTextBoxSelected();
        }
        #endregion
    }
}
