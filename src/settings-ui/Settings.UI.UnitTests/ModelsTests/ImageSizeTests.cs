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
        public void WhenUnitChangesToPercentFirstTime_WidthShouldBeSetTo100()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel);
            
            // Act
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert
            Assert.AreEqual(100.0, imageSize.Width, "Width should be set to 100 when switching to Percent unit for the first time");
        }
        
        [TestMethod]
        public void WhenUnitChangesToPercentFirstTime_HeightShouldBeSetTo100()
        {
            // Arrange
            var imageSize = new ImageSize(1, "Test", ResizeFit.Stretch, 854, 480, ResizeUnit.Pixel);
            
            // Act
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert
            Assert.AreEqual(100.0, imageSize.Height, "Height should be set to 100 when switching to Percent unit for the first time");
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
        
        [TestMethod]
        public void WhenSwitchingBackToPercent_PreviousPercentValuesShouldBeRestored()
        {
            // Arrange - Start with 50% width, 75% height
            var imageSize = new ImageSize(1, "Test", ResizeFit.Stretch, 50, 75, ResizeUnit.Percent);
            
            // Act - Switch to Pixel (values become 50 and 75 pixels)
            imageSize.Unit = ResizeUnit.Pixel;
            
            // Change the pixel values
            imageSize.Width = 1920;
            imageSize.Height = 1080;
            
            // Switch back to Percent
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert - Should restore the previous percent values (50, 75), not the pixel values
            Assert.AreEqual(50.0, imageSize.Width, "Width should be restored to previous percent value (50) when switching back to Percent");
            Assert.AreEqual(75.0, imageSize.Height, "Height should be restored to previous percent value (75) when switching back to Percent");
        }
        
        [TestMethod]
        public void WhenModifyingPercentValues_NewValuesShouldBeRemembered()
        {
            // Arrange - Start with default 100%
            var imageSize = new ImageSize(1, "Test", ResizeFit.Stretch, 1920, 1080, ResizeUnit.Pixel);
            imageSize.Unit = ResizeUnit.Percent;
            
            // Modify to 60% width, 80% height
            imageSize.Width = 60;
            imageSize.Height = 80;
            
            // Act - Switch to Pixel and back
            imageSize.Unit = ResizeUnit.Pixel;
            imageSize.Unit = ResizeUnit.Percent;
            
            // Assert - Should remember the modified values (60, 80)
            Assert.AreEqual(60.0, imageSize.Width, "Modified percent width (60) should be restored");
            Assert.AreEqual(80.0, imageSize.Height, "Modified percent height (80) should be restored");
        }
        
        [TestMethod]
        public void WhenConstructedWithPercent_ValuesShouldBePreserved()
        {
            // Arrange & Act
            var imageSize = new ImageSize(1, "Test", ResizeFit.Fit, 50, 75, ResizeUnit.Percent);
            
            // Assert
            Assert.AreEqual(50.0, imageSize.Width, "Width should be preserved when constructed with Percent unit");
            Assert.AreEqual(75.0, imageSize.Height, "Height should be preserved when constructed with Percent unit");
        }
    }
}