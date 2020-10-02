// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Common;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Markdown
{
    /// <summary>
    /// Implementation of preview handler for markdown files.
    /// </summary>
    [Guid("45769bcc-e8fd-42d0-947e-02beef77a1f5")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MarkdownPreviewHandler : FileBasedPreviewHandler, IDisposable
    {
        private MarkdownPreviewHandlerControl _markdownPreviewHandlerControl;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownPreviewHandler"/> class.
        /// </summary>
        public MarkdownPreviewHandler()
        {
            Initialize();
        }

        /// <inheritdoc />
        public override void DoPreview()
        {
            _markdownPreviewHandlerControl.DoPreview(FilePath);
        }

        /// <inheritdoc />
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.MarkdownFileHandlerLoaded());
            _markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl();

            return _markdownPreviewHandlerControl;
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
                   _markdownPreviewHandlerControl.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
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
