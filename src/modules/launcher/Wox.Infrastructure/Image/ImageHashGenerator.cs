// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Image
{
    public class ImageHashGenerator : IImageHashGenerator
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "Level of protection needed for the image data does not require a security guarantee")]
        public string GetHashFromImage(ImageSource image)
        {
            if (!(image is BitmapSource bitmapSource))
            {
                return null;
            }

            try
            {
                using (var outStream = new MemoryStream())
                {
                    // PngBitmapEncoder enc2 = new PngBitmapEncoder();
                    // enc2.Frames.Add(BitmapFrame.Create(tt));
                    var enc = new JpegBitmapEncoder();
                    var bitmapFrame = BitmapFrame.Create(bitmapSource);
                    bitmapFrame.Freeze();
                    enc.Frames.Add(bitmapFrame);
                    enc.Save(outStream);
                    var byteArray = outStream.GetBuffer();
                    return Convert.ToBase64String(SHA1.HashData(byteArray));
                }
            }
            catch (System.Exception e)
            {
                Log.Exception($"Failed to get hash from image", e, MethodBase.GetCurrentMethod().DeclaringType);
                return null;
            }
        }
    }
}
