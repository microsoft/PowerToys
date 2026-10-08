// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Settings.UI.UnitTests
{
    [TestClass]
    public class OOBEPageControlTests
    {
        private const string BaseDirectory = @"C:\Program Files\PowerToys\WinUI3Apps\";

        [TestMethod]
        public void ResolveHeroVideoPathMapsAppxUriToApplicationDirectory()
        {
            string path = OOBEPageControl.ResolveHeroVideoPath("ms-appx:///Assets/Settings/Modules/OOBE/AdvancedPaste.mp4", BaseDirectory);

            Assert.AreEqual(@"C:\Program Files\PowerToys\WinUI3Apps\Assets\Settings\Modules\OOBE\AdvancedPaste.mp4", path);
        }

        [TestMethod]
        public void ResolveHeroVideoPathIsCaseInsensitiveAndUnescapesAppxUri()
        {
            string path = OOBEPageControl.ResolveHeroVideoPath("MS-APPX:///Assets/My%20Video.mp4", BaseDirectory);

            Assert.AreEqual(@"C:\Program Files\PowerToys\WinUI3Apps\Assets\My Video.mp4", path);
        }

        [TestMethod]
        public void ResolveHeroVideoPathAcceptsRelativePath()
        {
            string path = OOBEPageControl.ResolveHeroVideoPath("Assets/Settings/Modules/OOBE/Run.mp4", BaseDirectory);

            Assert.AreEqual(@"C:\Program Files\PowerToys\WinUI3Apps\Assets\Settings\Modules\OOBE\Run.mp4", path);
        }

        [TestMethod]
        public void ResolveHeroVideoPathKeepsAbsolutePath()
        {
            string path = OOBEPageControl.ResolveHeroVideoPath(@"D:\media\Run.mp4", BaseDirectory);

            Assert.AreEqual(@"D:\media\Run.mp4", path);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void ResolveHeroVideoPathReturnsNullForEmptySource(string source)
        {
            Assert.IsNull(OOBEPageControl.ResolveHeroVideoPath(source, BaseDirectory));
        }

        [TestMethod]
        public void ComputeCenteredFillBoundsCropsWideVideoEvenlyOnBothSides()
        {
            // 1232x436 hero video in a 711x280 band: scale by height, crop the sides equally.
            Rect bounds = OOBEPageControl.ComputeCenteredFillBounds(711, 280, 1232, 436);

            double scale = 280.0 / 436;
            Assert.AreEqual(1232 * scale, bounds.Width, 0.001);
            Assert.AreEqual(280, bounds.Height, 0.001);
            Assert.AreEqual((711 - bounds.Width) / 2, bounds.X, 0.001);
            Assert.AreEqual(0, bounds.Y, 0.001);
            Assert.IsTrue(bounds.X < 0);
        }

        [TestMethod]
        public void ComputeCenteredFillBoundsCropsTallVideoEvenlyTopAndBottom()
        {
            Rect bounds = OOBEPageControl.ComputeCenteredFillBounds(1200, 280, 1232, 436);

            double scale = 1200.0 / 1232;
            Assert.AreEqual(1200, bounds.Width, 0.001);
            Assert.AreEqual(436 * scale, bounds.Height, 0.001);
            Assert.AreEqual(0, bounds.X, 0.001);
            Assert.AreEqual((280 - bounds.Height) / 2, bounds.Y, 0.001);
        }

        [TestMethod]
        public void ComputeCenteredFillBoundsFillsHostWhenVideoSizeUnknown()
        {
            Rect bounds = OOBEPageControl.ComputeCenteredFillBounds(711, 280, 0, 0);

            Assert.AreEqual(new Rect(0, 0, 711, 280), bounds);
        }
    }
}
