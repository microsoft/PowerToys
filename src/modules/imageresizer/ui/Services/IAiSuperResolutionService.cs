// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media.Imaging;

namespace ImageResizer.Services
{
    public interface IAiSuperResolutionService
    {
        BitmapSource ApplySuperResolution(BitmapSource source, int scale, AiSuperResolutionContext context);
    }
}
