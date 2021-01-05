// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI;

namespace Wox.Plugin
{
    /// <summary>
    /// Public APIs that plugin can use
    /// </summary>
    public interface IPublicAPI
    {
        /// <summary>
        /// Change Wox query
        /// </summary>
        /// <param name="query">query text</param>
        /// <param name="requery">
        /// force requery By default, Wox will not fire query if your query is same with existing one.
        /// Set this to true to force Wox requerying
        /// </param>
        void ChangeQuery(string query, bool requery = false);

        /// <summary>
        /// Restart Wox
        /// </summary>
        void RestartApp();

        /// <summary>
        /// Get current theme
        /// </summary>
        Theme GetCurrentTheme();

        /// <summary>
        /// Theme change event
        /// </summary>
        event ThemeChangedHandler ThemeChanged;

        /// <summary>
        /// Save all Wox settings
        /// </summary>
        void SaveAppAllSettings();

        /// <summary>
        /// Reloads any Plugins that have the
        /// IReloadable implemented. It refreshes
        /// Plugin's in memory data with new content
        /// added by user.
        /// </summary>
        void ReloadAllPluginData();

        /// <summary>
        /// Check for new Wox update
        /// </summary>
        void CheckForNewUpdate();

        /// <summary>
        /// Show message box
        /// </summary>
        /// <param name="title">Message title</param>
        /// <param name="subTitle">Message subtitle</param>
        /// <param name="iconPath">Message icon path (relative path to your plugin folder)</param>
        void ShowMsg(string title, string subTitle = "", string iconPath = "", bool useMainWindowAsOwner = true);

        /// <summary>
        /// Install Wox plugin
        /// </summary>
        /// <param name="path">Plugin path (ends with .wox)</param>
        void InstallPlugin(string path);

        /// <summary>
        /// Get all loaded plugins
        /// </summary>
        List<PluginPair> GetAllPlugins();

        /// <summary>
        /// Show toast notification
        /// </summary>
        /// <param name="text">Notification text</param>
        void ShowNotification(string text);
    }
}
