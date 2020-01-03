using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wox.Infrastructure.Image
{
    public interface IImageHashGenerator
    {
        string GetHashFromImage(ImageSource image);
    }
    public class ImageHashGenerator : IImageHashGenerator
    {
        public string GetHashFromImage(ImageSource imageSource)
        {
            if (!(imageSource is BitmapSource image))
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
                    enc.Frames.Add(BitmapFrame.Create(image));
                    enc.Save(outStream);
                    var byteArray = outStream.GetBuffer();
                    using (var sha1 = new SHA1CryptoServiceProvider())
                    {
                        var hash = Convert.ToBase64String(sha1.ComputeHash(byteArray));
                        return hash;
                    }
                }
            }
            catch
            {
                return null;
            }

        }
    }
}