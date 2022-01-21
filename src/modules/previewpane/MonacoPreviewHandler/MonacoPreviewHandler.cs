// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    using System;
    using System.Runtime.InteropServices;
    using Common;

    /// <summary>
    /// Implementation of preview handler for files with source code.
    /// </summary>
    [Guid("afbd5a44-2520-4ae0-9224-6cfce8fe4400")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MonacoPreviewHandler : FileBasedPreviewHandler, IDisposable
    {
        private MonacoPreviewHandlerControl _monacoPreviewHandlerControl;
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonacoPreviewHandler"/> class.
        /// </summary>
        public MonacoPreviewHandler()
        {
            this.Initialize();
        }

        /// <inheritdoc />
        [STAThread]
        public override void DoPreview()
        {
            _monacoPreviewHandlerControl.DoPreview(FilePath);
        }

        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            _monacoPreviewHandlerControl = new MonacoPreviewHandlerControl();

            return _monacoPreviewHandlerControl;
        }

        /// <summary>
        /// Disposes objects
        /// </summary>
        /// <param name="disposing">Is Disposing</param>
        [STAThread]
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _monacoPreviewHandlerControl.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        [STAThread]
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
