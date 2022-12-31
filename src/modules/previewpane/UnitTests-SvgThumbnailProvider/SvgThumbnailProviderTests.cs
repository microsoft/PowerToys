// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Common.ComInterlop;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.PowerToys.ThumbnailHandler.Svg;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(svgBuilder.ToString(), 256);

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
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(svgBuilder.ToString(), 256);
            Assert.IsTrue(thumbnail != null);
        }

        [TestMethod]
        public void CheckNoSvgShouldReturnNullBitmap()
        {
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<p>foo</p>");

            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(svgBuilder.ToString(), 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckNoSvgEmptyStringShouldReturnNullBitmap()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(string.Empty, 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckNoSvgNullStringShouldReturnNullBitmap()
        {
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void CheckZeroSizedThumbnailShouldReturnNullBitmap()
        {
            string content = "<svg></svg>";
            SvgThumbnailProvider svgThumbnailProvider = new SvgThumbnailProvider(null);
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(content, 0);
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
            Bitmap thumbnail = svgThumbnailProvider.GetThumbnail(svgBuilder.ToString(), 256);
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
    }
}
