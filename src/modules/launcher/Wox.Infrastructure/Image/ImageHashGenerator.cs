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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing this to enable FxCop. We are logging the exception, and going forward general exceptions should not be caught")]
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

                    using (var sha1 = SHA1.Create())
                    {
                        var hash = Convert.ToBase64String(sha1.ComputeHash(byteArray));
                        return hash;
                    }
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
