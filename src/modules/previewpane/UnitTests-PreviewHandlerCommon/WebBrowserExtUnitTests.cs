// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Common;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PreviewHandlerCommon;

namespace PreviewHandlerCommonUnitTests
{
    [STATestClass]
    public class WebBrowserExtUnitTests : WebBrowserExt
    {
        private const string DISPIDAMBIENTDLCONTROL = "[DISPID=-5512]";

        [TestMethod]
        public void InvokeMemberShouldSetValidFlagsWhenCalledWithValidDispId()
        {
            // Arrange
            var extendedSite = CreateWebBrowserSiteBase() as WebBrowserSiteExt;

            // Act
            var actualFlags = (int)extendedSite.InvokeMember(DISPIDAMBIENTDLCONTROL, BindingFlags.InvokeMethod, null, null, null, null, null, null);

            // Assert
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.PRAGMA_NO_CACHE) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.FORCEOFFLINE) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_CLIENTPULL) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_SCRIPTS) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_JAVA) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_FRAMEDOWNLOAD) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NOFRAMES) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_DLACTIVEXCTLS) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_RUNACTIVEXCTLS) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_BEHAVIORS) >= 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.SILENT) >= 0);
        }

        [TestMethod]
        public void InvokeMemberShouldOnlySetValidFlagsWhenCalledWithValidDispId()
        {
            // Arrange
            var extendedSite = CreateWebBrowserSiteBase() as WebBrowserSiteExt;

            // Act
            var actualFlags = (int)extendedSite.InvokeMember(DISPIDAMBIENTDLCONTROL, BindingFlags.InvokeMethod, null, null, null, null, null, null);

            // Assert
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.VIDEOS) == 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.BGSOUNDS) == 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.DOWNLOADONLY) == 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.RESYNCHRONIZE) == 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.NO_METACHARSET) == 0);
            Assert.IsTrue((actualFlags & (int)WebBrowserDownloadControlFlags.URL_ENCODING_DISABLE_UTF8) == 0);
            Assert.IsTrue((actualFlags & (uint)WebBrowserDownloadControlFlags.URL_ENCODING_ENABLE_UTF8) == 0);
        }
    }
}
