// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common;
using Common.Utilities;

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
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                WebBrowser browser = new WebBrowser();
                var stream = dataSource as IStream;
                using (var streamWrapper = new StreamWrapper(stream))
                {
                    using (var reader = new StreamReader(streamWrapper))
                    {
                        browser.DocumentText = reader.ReadToEnd();
                    }
                }

                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                this.Controls.Add(browser);
                base.DoPreview(dataSource);
            });
        }
    }
}
