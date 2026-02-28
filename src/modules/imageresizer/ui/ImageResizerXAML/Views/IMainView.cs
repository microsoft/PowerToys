// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageResizer.Views
{
    public interface IMainView
    {
        Task<IEnumerable<string>> OpenPictureFilesAsync();

        void Close();
    }
}
