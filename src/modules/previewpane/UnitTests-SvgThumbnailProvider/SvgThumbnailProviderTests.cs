// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.PowerToys.ThumbnailHandler.Svg;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SvgThumbnailProviderUnitTests
{
    [STATestClass]
    public class SvgThumbnailProviderTests
    {
        private bool BitmapsAreEqual(Bitmap bmp1, Bitmap bmp2)
        {
            if (bmp1 == null || bmp2 == null)
            {
                return false;
            }

            bool ignoreAlpha = Image.IsAlphaPixelFormat(bmp1.PixelFormat) != Image.IsAlphaPixelFormat(bmp2.PixelFormat);

            if (bmp1.Size != bmp2.Size)
            {
                return false;
            }

            BitmapData data1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width, bmp1.Height), ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData data2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width, bmp2.Height), ImageLockMode.ReadOnly, bmp2.PixelFormat);

            int bytesPerPixel = Image.GetPixelFormatSize(bmp1.PixelFormat) / 8;
            bool areEqual = true;
            int byteCount = data1.Stride * bmp1.Height;
            byte[] bytes1 = new byte[byteCount];
            byte[] bytes2 = new byte[byteCount];

            Marshal.Copy(data1.Scan0, bytes1, 0, byteCount);
            Marshal.Copy(data2.Scan0, bytes2, 0, byteCount);

            for (int i = 0; i < byteCount; i += bytesPerPixel)
            {
                for (int j = 0; j < bytesPerPixel; j++)
                {
                    if (j == 0 && ignoreAlpha)
                    {
                        continue; // Assuming alpha is the first byte
                    }

                    if (bytes1[i + j] != bytes2[i + j])
                    {
                        areEqual = false;
                        break;
                    }
                }

                if (!areEqual)
                {
                    break;
                }
            }

            bmp1.UnlockBits(data1);
            bmp2.UnlockBits(data2);
            return areEqual;
        }

        [TestMethod]
        public void LoadSimpleSVGShouldReturnNonNullBitmap()
        {
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = svgBuilder.ToString();
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);

            Assert.IsNotNull(thumbnail);
            Assert.IsTrue(thumbnail.Width > 0);
            Assert.IsTrue(thumbnail.Height > 0);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnNonNullBitmapIfBlockedElementsIsPresentInNestedLevel()
        {
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t\t<script>alert(\"valid-message\")</script>");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = svgBuilder.ToString();
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void CheckThatWidthAndHeightAreParsedCorrectly1()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = @"
<svg
   xmlns:dc=""http://purl.org/dc/elements/1.1/""
    xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
   xmlns:svg=""http://www.w3.org/2000/svg""
   xmlns=""http://www.w3.org/2000/svg""
   xmlns:xlink=""http://www.w3.org/1999/xlink""
   id=""svg8""
   version=""1.1""
   viewBox=""0 0 380.99999 304.79999"" width=""1px"" height=""20pt"" >
";

            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void CheckThatWidthAndHeightAreParsedCorrectly2()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = @"

<svg
   xmlns:dc=""http://purl.org/dc/elements/1.1/""
    xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
   xmlns:svg=""http://www.w3.org/2000/svg""
   xmlns=""http://www.w3.org/2000/svg""
   xmlns:xlink=""http://www.w3.org/1999/xlink""
   id=""svg8""
   version=""1.1""
   height=""1152"" width=""2000vh"" >
";

            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void CheckThatWidthAndHeightAreParsedCorrectly3()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = @"

<svg
   xmlns:dc=""http://purl.org/dc/elements/1.1/""
    xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
   xmlns:svg=""http://www.w3.org/2000/svg""
   xmlns=""http://www.w3.org/2000/svg""
   xmlns:xlink=""http://www.w3.org/1999/xlink""
   id=""svg8""
   version=""1.1""
   viewBox=""0 0 380.99999 304.79999"" width=""2000"" >
";
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void CheckNoSvgShouldReturnNullBitmap()
        {
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<p>foo</p>");

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = svgBuilder.ToString();
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckNoSvgEmptyStringShouldReturnNullBitmap()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = string.Empty;
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckNoSvgNullStringShouldReturnNullBitmap()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = string.Empty;
            svgThumbnailProvider.SvgContentsReady.Set();

            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckZeroSizedThumbnailShouldReturnNullBitmap()
        {
            string content = "<svg></svg>";
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = content;
            svgThumbnailProvider.SvgContentsReady.Set();
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(0);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnBitmapHTMLWrapped()
        {
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<html>");
            svgBuilder.AppendLine("<head>");
            svgBuilder.AppendLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=Edge\">");
            svgBuilder.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html\" charset=\"utf-8\">");
            svgBuilder.AppendLine("</head>");
            svgBuilder.AppendLine("<body>");
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("</circle>");
            svgBuilder.AppendLine("</svg>");
            svgBuilder.AppendLine("</body>");
            svgBuilder.AppendLine("</html>");

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            svgThumbnailProvider.SvgContents = svgBuilder.ToString();
            svgThumbnailProvider.SvgContentsReady.Set();

            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void GetThumbnailValidStreamSVG()
        {
            var filePath = "HelperFiles/file1.svg";

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(filePath);

            Bitmap bitmap = svgThumbnailProvider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailValidStreamHTML()
        {
            var filePath = "HelperFiles/file2.svg";

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(filePath);

            Bitmap bitmap = svgThumbnailProvider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void SvgCommentsAreHandledCorrectly()
        {
            var filePath = "HelperFiles/WithComments.svg";

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(filePath);

            Bitmap bitmap = svgThumbnailProvider.GetThumbnail(8);

            var expectedBitmap = new Bitmap("HelperFiles/WithComments_8.bmp");

            Assert.IsTrue(BitmapsAreEqual(expectedBitmap, bitmap));
        }
    }
}
