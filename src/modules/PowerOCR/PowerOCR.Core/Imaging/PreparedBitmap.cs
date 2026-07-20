// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace PowerOCR.Core.Imaging;

public sealed class PreparedBitmap : IDisposable
{
    public PreparedBitmap(Bitmap bitmap, double scale, int offsetX, int offsetY)
    {
        Bitmap = bitmap;
        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public Bitmap Bitmap { get; }

    public double Scale { get; }

    public int OffsetX { get; }

    public int OffsetY { get; }

    public void Dispose() => Bitmap.Dispose();
}
