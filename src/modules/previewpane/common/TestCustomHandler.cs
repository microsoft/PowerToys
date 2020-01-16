// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Common
{
    /// <summary>
    /// This is a test custom handler to check the implemnatation will be removed in future PR.
    /// </summary>
    [PreviewHandler("SvgPreviewHandler", ".svg", "{88235ab2-bfce-4be8-9ed0-0408cd8da792}")]
    [ProgId("SvgPreviewHandler")]
    [Guid("22a1a8e8-e929-4732-90ce-91eaff38b614")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class TestCustomHandler : FileBasedPreviewHandler
    {
        private CustomControlTest previewHandlerControl;

        /// <summary>
        /// Todo.
        /// </summary>
        public override void DoPreview()
        {
            this.previewHandlerControl.DoPreview(this.FilePath);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns>Toddo.</returns>
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            this.previewHandlerControl = new CustomControlTest();
            return this.previewHandlerControl;
        }
    }
}
