// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1636:FileHeaderCopyrightTextMustMatch", Justification = "File created under PowerToys.")]

namespace ImageResizer.Properties
{
    public class AppFixture : IDisposable
    {
        public AppFixture()
        {
            // new App() needs to be created since Settings.Reload() uses App.Current to update properties on the UI thread. App() can be created only once otherwise it results in System.InvalidOperationException : Cannot create more than one System.Windows.Application instance in the same AppDomain.
            _imageResizerApp = new App();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "new App() needs to be created since Settings.Reload() uses App.Current to update properties on the UI thread. App() can be created only once otherwise it results in System.InvalidOperationException : Cannot create more than one System.Windows.Application instance in the same AppDomain")]
        private App _imageResizerApp;
        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _imageResizerApp.Dispose();
                    _imageResizerApp = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
