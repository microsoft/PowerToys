// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;

namespace ImageResizer.Views
{
    public interface IMainView
    {
        void Close();

        IEnumerable<string> OpenPictureFiles();
    }
}
