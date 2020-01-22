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
        private Stream dataSourceStream;

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                WebBrowser browser = new WebBrowser();
                this.dataSourceStream = new StreamWrapper(dataSource as IStream);

                browser.DocumentStream = this.dataSourceStream;
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                browser.ScriptErrorsSuppressed = true;
                browser.ScrollBarsEnabled = true;
                this.Controls.Add(browser);
                base.DoPreview(dataSource);
            });
        }

        /// <summary>
        /// Free resources on the unload of Preview.
        /// </summary>
        public override void Unload()
        {
            base.Unload();
            if (this.dataSourceStream != null)
            {
                this.dataSourceStream.Dispose();
                this.dataSourceStream = null;
            }
        }
    }
}
