// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace PowerOCR.Core.Imaging;

public interface IBitmapPreprocessor
{
    Size GetOutputSize(Bitmap source, double scale);

    PreparedBitmap Prepare(Bitmap source, double scale);
}
