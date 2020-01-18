// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Extends <see cref="FileBasedPreviewHandler"/> for Svg Preview Handler.
    /// </summary>
    [PreviewHandler("SvgPreviewHandler", ".svg", "{88235ab2-bfce-4be8-9ed0-0408cd8da792}")]
    [ProgId("SvgPreviewHandler")]
    [Guid("22a1a8e8-e929-4732-90ce-91eaff38b614")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SvgPreviewHandler : FileBasedPreviewHandler
    {
        private SvgPreviewControl svgPreviewControl;

        /// <inheritdoc/>
        public override void DoPreview()
        {
            this.svgPreviewControl.DoPreview(this.FilePath);
        }

        /// <inheritdoc/>
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            this.svgPreviewControl = new SvgPreviewControl();
            return this.svgPreviewControl;
        }
    }
}
