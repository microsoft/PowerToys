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
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                var filePath = dataSource as string;
                WebBrowser browser = new WebBrowser();

                browser.DocumentText = "Test";
                browser.Navigate(filePath);
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                this.Controls.Add(browser);
                base.DoPreview(dataSource);
            });
        }
    }
}
