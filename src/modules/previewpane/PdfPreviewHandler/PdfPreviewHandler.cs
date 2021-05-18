// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Pdf
{
    /// <summary>
    /// Implementation of preview handler for pdf files.
    /// </summary>
    [Guid("07665729-6243-4746-95b7-79579308d1b2")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class PdfPreviewHandler : StreamBasedPreviewHandler, IDisposable
    {
        private PdfPreviewHandlerControl _pdfPreviewHandlerControl;
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfPreviewHandler"/> class.
        /// </summary>
        public PdfPreviewHandler()
        {
            Initialize();
        }

        /// <inheritdoc />
        public override void DoPreview()
        {
            _pdfPreviewHandlerControl.DoPreview(Stream);
        }

        /// <inheritdoc />
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.PdfFileHandlerLoaded());
            _pdfPreviewHandlerControl = new PdfPreviewHandlerControl();

            return _pdfPreviewHandlerControl;
        }

        /// <summary>
        /// Disposes objects
        /// </summary>
        /// <param name="disposing">Is Disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _pdfPreviewHandlerControl.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
