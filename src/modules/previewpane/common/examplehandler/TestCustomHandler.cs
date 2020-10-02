// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common
{
    /// <summary>
    /// This is a example custom handler to show how to extend the library.
    /// </summary>
    [Guid("22a1a8e8-e929-4732-90ce-91eaff38b614")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class TestCustomHandler : FileBasedPreviewHandler, IDisposable
    {
        private CustomControlTest _previewHandlerControl;
        private bool disposedValue;

        /// <inheritdoc />
        public override void DoPreview()
        {
            _previewHandlerControl.DoPreview(FilePath);
        }

        /// <inheritdoc />
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            _previewHandlerControl = new CustomControlTest();

            return _previewHandlerControl;
        }

        /// <summary>
        /// Disposes objects
        /// </summary>
        /// <param name="disposing">Is Disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _previewHandlerControl.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
