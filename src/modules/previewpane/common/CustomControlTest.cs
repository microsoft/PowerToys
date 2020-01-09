// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Windows.Forms;

namespace Common
{
    /// <summary>
    /// Todl.
    /// </summary>
    public class CustomControlTest : FormHandlerControl
    {
        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        public void DoPreview(string filePath)
        {
            this.InvokeOnControlThread(() =>
            {
                this.Visible = true;
                var text = File.ReadAllText(filePath);
                WebBrowser browser = new WebBrowser();
                browser.DocumentText = text;
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                browser.AllowWebBrowserDrop = false;
                browser.Navigating += new WebBrowserNavigatingEventHandler((sender, e) => { e.Cancel = true; });
                browser.Invalidate();
                this.Controls.Add(browser);
            });
        }
    }
}
