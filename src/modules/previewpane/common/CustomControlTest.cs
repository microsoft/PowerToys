// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Windows.Forms;

namespace Common
{
    /// <summary>
    /// This is test custom control to test the implementation.
    /// </summary>
    public class CustomControlTest : FormHandlerControl
    {
        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        public void DoPreview(string filePath)
        {
            this.InvokeOnControlThread(() =>
            {
                this.Visible = true;
                WebBrowser browser = new WebBrowser();

                // browser.Navigate(filePath);
                browser.DocumentText = "Test";
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                this.Controls.Add(browser);
            });
        }
    }
}
