// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using Common;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                this.Enabled = false;
                var filePath = dataSource as string;
                WebBrowser browser = new WebBrowser();
                browser.Navigate(filePath);
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                this.Controls.Add(browser);
                base.DoPreview(dataSource);
            });
        }
    }
}
