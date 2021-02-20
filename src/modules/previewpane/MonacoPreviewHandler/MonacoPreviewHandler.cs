// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;
using MonacoPreviewHandler;

namespace XYZPreviewHandler
{
    /// <summary>
    /// Implementation of preview handler for .xyz files.
    /// GUID = CLSID / CLASS ID.
    /// </summary>
    [Guid("fd1aa683-4961-4836-a170-b968a8861002")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MonacoPreviewHandler : FileBasedPreviewHandler
    {
        private MonacoPreviewHandlerControl MonacoPreviewHandlerControl;

        /// Call your rendering method here.
        public override void DoPreview()
        {
            this.MonacoPreviewHandlerControl.DoPreview(this.FilePath);
        }

        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            this.MonacoPreviewHandlerControl = new MonacoPreviewHandlerControl();
            return this.MonacoPreviewHandlerControl;
        }
    }
}
