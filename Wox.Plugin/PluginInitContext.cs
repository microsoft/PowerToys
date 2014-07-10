using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin
{
    public class PluginInitContext
    {
        public PluginMetadata CurrentPluginMetadata { get; set; }

        /// <summary>
        /// Public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }

        public HttpProxy Proxy { get; set; }

        #region Legacy APIs

        [Obsolete("This method has been obsoleted, use API.ShellRun instead")]
        public bool ShellRun(string cmd)
        {
            return API.ShellRun(cmd);
        }

        [Obsolete("This method has been obsoleted, use API.OpenSettingDialog instead")]
        public void ChangeQuery(string query, bool requery = false)
        {
            API.ChangeQuery(query, requery);
        }

        [Obsolete("This method has been obsoleted, use API.CloseApp instead")]
        public void CloseApp()
        {
            API.CloseApp();
        }

        [Obsolete("This method has been obsoleted, use API.HideApp instead")]
        public void HideApp()
        {
            API.HideApp();
        }

        [Obsolete("This method has been obsoleted, use API.ShowApp instead")]
        public void ShowApp()
        {
            API.ShowApp();
        }

        [Obsolete("This method has been obsoleted, use API.OpenSettingDialog instead")]
        public void ShowMsg(string title, string subTitle, string iconPath)
        {
            API.ShowMsg(title, subTitle, iconPath);
        }

        [Obsolete("This method has been obsoleted, use API.OpenSettingDialog instead")]
        public void OpenSettingDialog()
        {
            API.OpenSettingDialog();
        }

        [Obsolete("This method has been obsoleted, use API.ShowCurrentResultItemTooltip instead")]
        public void ShowCurrentResultItemTooltip(string tooltip)
        {
            API.ShowCurrentResultItemTooltip(tooltip);
        }

        [Obsolete("This method has been obsoleted, use API.StartLoadingBar instead")]
        public void StartLoadingBar()
        {
            API.StartLoadingBar();
        }

        [Obsolete("This method has been obsoleted, use API.StopLoadingBar instead")]
        public void StopLoadingBar()
        {
            API.StopLoadingBar();
        }

        [Obsolete("This method has been obsoleted, use API.InstallPlugin instead")]
        public void InstallPlugin(string path)
        {
            API.InstallPlugin(path);
        }

        [Obsolete("This method has been obsoleted, use API.ReloadPlugins instead")]
        public void ReloadPlugins()
        {
            API.ReloadPlugins();
        }

        #endregion
    }
}
