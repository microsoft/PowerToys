// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common
{
    /// <summary>
    /// Test the implementation.
    /// </summary>
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
