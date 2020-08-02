using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using PowerLauncher.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Image;
using Wox.Plugin;
using PowerLauncher.ViewModel;

namespace Wox
{
    public class PublicAPIInstance : IPublicAPI, IDisposable
    {
        private readonly SettingWindowViewModel _settingsVM;
        private readonly MainViewModel _mainVM;
        private readonly Alphabet _alphabet;
        private bool _disposed = false;
        private readonly ThemeManager _themeManager;

        public event ThemeChangedHandler ThemeChanged;

        #region Constructor

        public PublicAPIInstance(SettingWindowViewModel settingsVM, MainViewModel mainVM, Alphabet alphabet, ThemeManager themeManager)
        {
            _settingsVM = settingsVM ?? throw new ArgumentNullException(nameof(settingsVM));
            _mainVM = mainVM ?? throw new ArgumentNullException(nameof(mainVM));
            _alphabet = alphabet ?? throw new ArgumentNullException(nameof(alphabet));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _themeManager.ThemeChanged += OnThemeChanged;
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
        }

        #endregion

        #region Public API

        public void ChangeQuery(string query, bool requery = false)
        {
            _mainVM.ChangeQueryText(query, requery);
        }

        public void RestartApp()
        {
            _mainVM.MainWindowVisibility = Visibility.Hidden;

            // we must manually save
            // UpdateManager.RestartApp() will call Environment.Exit(0)
            // which will cause ungraceful exit
            SaveAppAllSettings();

            // Todo : Implement logic to restart this app.
            Environment.Exit(0);
        }

        public void CheckForNewUpdate()
        {
            //_settingsVM.UpdateApp();
        }

        public void SaveAppAllSettings()
        {
            _mainVM.Save();
            _settingsVM.Save();
            PluginManager.Save();
            ImageLoader.Save();
            _alphabet.Save();
        }

        public void ReloadAllPluginData()
        {
            PluginManager.ReloadData();
        }

        public void ShowMsg(string title, string subTitle = "", string iconPath = "", bool useMainWindowAsOwner = true)
        {
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

        public Theme GetCurrentTheme()
        {
            return _themeManager.GetCurrentTheme();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Methods

        protected void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            ThemeChanged?.Invoke(oldTheme, newTheme);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _themeManager.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
        #endregion
    }
}
