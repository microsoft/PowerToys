// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.ModelsTests
{
    [TestClass]
    public class ImageSizeTests
    {
        [TestMethod]
        public void WhenUnitChangesToPercent_WidthShouldBeSetTo100()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel);
            
            // Act
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert
            Assert.AreEqual(100.0, imageSize.Width, "Width should be set to 100 when switching to Percent unit");
        }
        
        [TestMethod]
        public void WhenUnitChangesToPercent_HeightShouldBeSetTo100()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Stretch, 854, 480, ResizeUnit.Pixel);
            
            // Act
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert
            Assert.AreEqual(100.0, imageSize.Height, "Height should be set to 100 when switching to Percent unit");
        }
        
        [TestMethod]
        public void WhenUnitChangesFromPercentToPixel_ValuesShouldNotChange()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Fit, 50, 75, ResizeUnit.Percent);
            
            // Act
            imageSize.Unit = ResizeUnit.Pixel;
            
            // Assert
            Assert.AreEqual(50.0, imageSize.Width, "Width should remain unchanged when switching from Percent to other units");
            Assert.AreEqual(75.0, imageSize.Height, "Height should remain unchanged when switching from Percent to other units");
        }
        
        [TestMethod]
        public void WhenUnitRemainsPercent_ValuesShouldNotChange()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Fit, 75, 60, ResizeUnit.Percent);
            
            // Act
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert
            Assert.AreEqual(75.0, imageSize.Width, "Width should remain unchanged when unit stays as Percent");
            Assert.AreEqual(60.0, imageSize.Height, "Height should remain unchanged when unit stays as Percent");
        }
    }
}