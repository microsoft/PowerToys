// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Media.Imaging;

namespace ImageResizer.Services
{
    public interface IAISuperResolutionService : IDisposable
    {
        BitmapSource ApplySuperResolution(BitmapSource source, int scale, string filePath);
    }
}
