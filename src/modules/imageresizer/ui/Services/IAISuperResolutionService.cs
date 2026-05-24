// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics.Imaging;

namespace ImageResizer.Services
{
    public interface IAISuperResolutionService : IDisposable
    {
        SoftwareBitmap ApplySuperResolution(SoftwareBitmap source, int scale, string filePath);
    }
}
