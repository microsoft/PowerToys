// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace WorkspacesCsharpLibrary
{
    public class DrawHelper
    {
        public static void SaveBitmap(Bitmap bitmap, MemoryStream memory)
        {
            ImageCodecInfo imageCodecInfo = ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == ImageFormat.Png.Guid);
            EncoderParameters encoderParameters = new(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 50);

            bitmap.Save(memory, imageCodecInfo, encoderParameters);
        }
    }
}
