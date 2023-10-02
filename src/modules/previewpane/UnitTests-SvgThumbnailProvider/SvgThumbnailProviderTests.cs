// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

            Assert.IsTrue(bitmap != null);
        }
    }
}
