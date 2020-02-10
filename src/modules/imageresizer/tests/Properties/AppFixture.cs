// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;

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
