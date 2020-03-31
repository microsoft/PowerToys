// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Extends <see cref="StreamBasedPreviewHandler"/> for Svg Preview Handler.
    /// </summary>
    [Guid("ddee2b8a-6807-48a6-bb20-2338174ff779")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SvgPreviewHandler : StreamBasedPreviewHandler
    {
        private SvgPreviewControl svgPreviewControl;

        /// <inheritdoc/>
        public override void DoPreview()
        {
            this.svgPreviewControl.DoPreview(this.Stream);
        }

        /// <inheritdoc/>
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            this.svgPreviewControl = new SvgPreviewControl();
            return this.svgPreviewControl;
        }
    }
}
