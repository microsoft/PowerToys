// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;

namespace MarkdownPreviewHandler
{
    /// <summary>
    /// This is a example custom handler to show how to extend the library.
    /// </summary>
    [PreviewHandler("MarkdownPreviewPaneHandler", ".md", "{88235ab2-bfce-4be8-9ed0-0408cd8da792}")]
    [ProgId("MarkdownPreviewPaneHandler")]
    [Guid("f3f3cf27-3feb-417a-83af-1875967dbad2")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MarkdownPreviewHandler : FileBasedPreviewHandler
    {
        private MarkdownPreviewHandlerControl markdownPreviewHandlerControl;

        /// <inheritdoc />
        public override void DoPreview()
        {
            this.markdownPreviewHandlerControl.DoPreview(this.FilePath);
        }

        /// <inheritdoc />
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            this.markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl();
            return this.markdownPreviewHandlerControl;
        }
    }
}
