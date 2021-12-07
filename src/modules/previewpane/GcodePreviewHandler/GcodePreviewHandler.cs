// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Gcode
{
    /// <summary>
    /// Extends <see cref="StreamBasedPreviewHandler"/> for Gcode Preview Handler.
    /// </summary>
    [Guid("ec52dea8-7c9f-4130-a77b-1737d0418507")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class GcodePreviewHandler : StreamBasedPreviewHandler, IDisposable
    {
        private GcodePreviewHandlerControl _gcodePreviewControl;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="GcodePreviewHandler"/> class.
        /// </summary>
        public GcodePreviewHandler()
        {
            Initialize();
        }

        /// <inheritdoc/>
        public override void DoPreview()
        {
            _gcodePreviewControl.DoPreview(Stream);
        }

        /// <inheritdoc/>
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.GcodeFileHandlerLoaded());
            _gcodePreviewControl = new GcodePreviewHandlerControl();

            return _gcodePreviewControl;
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
                    _gcodePreviewControl.Dispose();
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
