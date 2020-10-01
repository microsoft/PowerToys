// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Svg
{
    /// <summary>
    /// Extends <see cref="StreamBasedPreviewHandler"/> for Svg Preview Handler.
    /// </summary>
    [Guid("ddee2b8a-6807-48a6-bb20-2338174ff779")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SvgPreviewHandler : StreamBasedPreviewHandler, IDisposable
    {
        private SvgPreviewControl _svgPreviewControl;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="SvgPreviewHandler"/> class.
        /// </summary>
        public SvgPreviewHandler()
        {
            Initialize();
        }

        /// <inheritdoc/>
        public override void DoPreview()
        {
            _svgPreviewControl.DoPreview(Stream);
        }

        /// <inheritdoc/>
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.SvgFileHandlerLoaded());
            _svgPreviewControl = new SvgPreviewControl();

            return _svgPreviewControl;
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
                    _svgPreviewControl.Dispose();
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
