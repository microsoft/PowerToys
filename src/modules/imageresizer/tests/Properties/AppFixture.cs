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
            imageResizerApp = new App();
        }

        public void Dispose()
        {
            imageResizerApp = null;
        }

        private App imageResizerApp;
    }
}
