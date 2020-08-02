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
            imageResizerApp = new App();
        }

        public void Dispose()
        {
            imageResizerApp = null;
        }

        private App imageResizerApp;
    }
}
