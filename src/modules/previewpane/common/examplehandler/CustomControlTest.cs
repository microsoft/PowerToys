// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Common
{
    /// <summary>
    /// This is test custom control to test the implementation.
    /// </summary>
    public class CustomControlTest : FormHandlerControl
    {
        /// <summary>
        /// WebView2 Control to display Svg.
        /// </summary>
        private WebView2 _browser;

        /// <summary>
        /// WebView2 Environment
        /// </summary>
        private CoreWebView2Environment _webView2Environment;

        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalCustomControlTest";

        /// <summary>
        /// Gets the path of the current assembly.
        /// </summary>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/283917/14774889
        /// </remarks>
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            var filePath = dataSource as string;

            _browser = new WebView2();
            _browser.Dock = DockStyle.Fill;
            _browser.Visible = true;
            _browser.NavigationCompleted += (object sender, CoreWebView2NavigationCompletedEventArgs args) =>
            {
                // Put here logic needed after WebView2 control is done navigating to url/page
            };

            ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
                webView2EnvironmentAwaiter = CoreWebView2Environment
                    .CreateAsync(userDataFolder: System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                                "\\AppData\\LocalLow\\Microsoft\\PowerToys\\CustomControlTest-Temp")
                    .ConfigureAwait(true).GetAwaiter();
            webView2EnvironmentAwaiter.OnCompleted(async () =>
            {
                try
                {
                    _webView2Environment = webView2EnvironmentAwaiter.GetResult();
                    await _browser.EnsureCoreWebView2Async(_webView2Environment).ConfigureAwait(true);
                    await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.addEventListener('contextmenu', window => {window.preventDefault();});");
                    _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AssemblyDirectory, CoreWebView2HostResourceAccessKind.Allow);

                    // Navigate to page represented as a string
                    _browser.NavigateToString("Test");

                    // Or navigate to Uri
                    _browser.Source = new Uri(filePath);
                }
                catch (NullReferenceException)
                {
                }
            });

            this.Controls.Add(_browser);
            base.DoPreview(dataSource);
        }
    }
}
