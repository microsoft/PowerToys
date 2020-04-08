// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;

namespace MarkdownPreviewHandler
{
    /// <summary>
    /// Implementation of preview handler for markdown files.
    /// </summary>
    [Guid("45769bcc-e8fd-42d0-947e-02beef77a1f5")]
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
