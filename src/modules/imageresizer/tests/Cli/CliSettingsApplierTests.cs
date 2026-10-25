// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ImageResizer.Cli;
using ImageResizer.Models;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Tests.Cli
{
    [TestClass]
    public class CliSettingsApplierTests
    {
        private Settings CreateDefaultSettings()
        {
            var settings = new Settings();
            settings.Sizes.Add(new ResizeSize(0, "Small", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(1, "Medium", ResizeFit.Fit, 1366, 768, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(2, "Large", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel));
            return settings;
        }

        [TestMethod]
        public void Apply_WithCustomWidth_SetsCustomSizeWidth()
        {
            var options = new CliOptions { Width = 800 };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Height = 321.5;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(800.0, settings.CustomSize.Width);
            Assert.AreEqual(0.0, settings.CustomSize.Height);
        }

        [TestMethod]
        public void Apply_WithCustomHeight_SetsCustomSizeHeight()
        {
            var options = new CliOptions { Height = 600 };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Width = 321.5;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(600.0, settings.CustomSize.Height);
            Assert.AreEqual(0.0, settings.CustomSize.Width);
        }

        [TestMethod]
        public void Apply_WithCustomSize_SelectsCustomSizeIndex()
        {
            var options = new CliOptions { Width = 800, Height = 600 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            // Custom size index should be settings.Sizes.Count
            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithZeroWidth_SetsZeroForAutoCalculation()
        {
            var options = new CliOptions { Width = 0, Height = 600 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(0.0, settings.CustomSize.Width);
            Assert.AreEqual(600.0, settings.CustomSize.Height);
        }

        [TestMethod]
        public void Apply_WithZeroHeight_SetsZeroForAutoCalculation()
        {
            var options = new CliOptions { Width = 800, Height = 0 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(800.0, settings.CustomSize.Width);
            Assert.AreEqual(0.0, settings.CustomSize.Height);
        }

        [TestMethod]
        public void Apply_WithNullWidthAndHeight_DoesNotModifyCustomSize()
        {
            var options = new CliOptions { Width = null, Height = null };
            var settings = CreateDefaultSettings();
            var originalWidth = settings.CustomSize.Width;
            var originalHeight = settings.CustomSize.Height;

            CliSettingsApplier.Apply(options, settings);

            // When both null, should not modify CustomSize (keeps default 1024x640)
            Assert.AreEqual(originalWidth, settings.CustomSize.Width);
            Assert.AreEqual(originalHeight, settings.CustomSize.Height);
        }

        [DataTestMethod]
        [DataRow(null, 2)]
        [DataRow(1, 1)]
        public void Apply_WithUnitAndFitWithoutDimensions_PreservesCustomSize(int? sizeIndex, int expectedSelectedSizeIndex)
        {
            var options = new CliOptions { Unit = ResizeUnit.Pixel, Fit = ResizeFit.Fill, SizeIndex = sizeIndex };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Unit = ResizeUnit.Inch;
            settings.CustomSize.Fit = ResizeFit.Stretch;
            settings.CustomSize.Width = 321.5;
            settings.CustomSize.Height = 123.5;
            settings.SelectedSizeIndex = 2;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeUnit.Inch, settings.CustomSize.Unit);
            Assert.AreEqual(ResizeFit.Stretch, settings.CustomSize.Fit);
            Assert.AreEqual(321.5, settings.CustomSize.Width);
            Assert.AreEqual(123.5, settings.CustomSize.Height);
            Assert.AreEqual(expectedSelectedSizeIndex, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithUnit_SetsCustomSizeUnit()
        {
            var options = new CliOptions { Width = 100, Unit = ResizeUnit.Percent };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeUnit.Percent, settings.CustomSize.Unit);
        }

        [TestMethod]
        public void Apply_WithFit_SetsCustomSizeFit()
        {
            var options = new CliOptions { Width = 800, Fit = ResizeFit.Fill };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeFit.Fill, settings.CustomSize.Fit);
        }

        [DataTestMethod]
        [DataRow(800.0, null, 800.0, 0.0)]
        [DataRow(null, 600.0, 0.0, 600.0)]
        [DataRow(800.0, 600.0, 800.0, 600.0)]
        public void Apply_WithDimensionsAndOmittedUnitAndFit_PreservesCustomPreferences(
            double? width, double? height, double expectedWidth, double expectedHeight)
        {
            var options = new CliOptions { Width = width, Height = height };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Unit = ResizeUnit.Inch;
            settings.CustomSize.Fit = ResizeFit.Stretch;
            settings.CustomSize.Width = 321.5;
            settings.CustomSize.Height = 123.5;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeUnit.Inch, settings.CustomSize.Unit);
            Assert.AreEqual(ResizeFit.Stretch, settings.CustomSize.Fit);
            Assert.AreEqual(expectedWidth, settings.CustomSize.Width);
            Assert.AreEqual(expectedHeight, settings.CustomSize.Height);
        }

        [DataTestMethod]
        [DataRow(800.0, null, 800.0, 0.0)]
        [DataRow(null, 600.0, 0.0, 600.0)]
        [DataRow(800.0, 600.0, 800.0, 600.0)]
        public void Apply_WithDimensionsAndFitButNoUnit_PreservesCustomUnit(
            double? width, double? height, double expectedWidth, double expectedHeight)
        {
            var options = new CliOptions { Width = width, Height = height, Fit = ResizeFit.Fill };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Unit = ResizeUnit.Inch;
            settings.CustomSize.Fit = ResizeFit.Stretch;
            settings.CustomSize.Width = 321.5;
            settings.CustomSize.Height = 123.5;
            settings.SelectedSizeIndex = 2;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeUnit.Inch, settings.CustomSize.Unit);
            Assert.AreEqual(ResizeFit.Fill, settings.CustomSize.Fit);
            Assert.AreEqual(expectedWidth, settings.CustomSize.Width);
            Assert.AreEqual(expectedHeight, settings.CustomSize.Height);
            Assert.AreEqual(7, settings.SelectedSizeIndex);
        }

        [DataTestMethod]
        [DataRow(800.0, null, 800.0, 0.0)]
        [DataRow(null, 600.0, 0.0, 600.0)]
        [DataRow(800.0, 600.0, 800.0, 600.0)]
        public void Apply_WithDimensionsAndUnitButNoFit_PreservesCustomFit(
            double? width, double? height, double expectedWidth, double expectedHeight)
        {
            var options = new CliOptions { Width = width, Height = height, Unit = ResizeUnit.Pixel };
            var settings = CreateDefaultSettings();
            settings.CustomSize.Unit = ResizeUnit.Inch;
            settings.CustomSize.Fit = ResizeFit.Stretch;
            settings.CustomSize.Width = 321.5;
            settings.CustomSize.Height = 123.5;
            settings.SelectedSizeIndex = 2;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(ResizeUnit.Pixel, settings.CustomSize.Unit);
            Assert.AreEqual(ResizeFit.Stretch, settings.CustomSize.Fit);
            Assert.AreEqual(expectedWidth, settings.CustomSize.Width);
            Assert.AreEqual(expectedHeight, settings.CustomSize.Height);
            Assert.AreEqual(7, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithValidSizeIndex_SetsSelectedSizeIndex()
        {
            var options = new CliOptions { SizeIndex = 1 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(1, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithInvalidSizeIndex_DoesNotChangeSelection()
        {
            var options = new CliOptions { SizeIndex = 99 };
            var settings = CreateDefaultSettings();
            var originalIndex = settings.SelectedSizeIndex;

            CliSettingsApplier.Apply(options, settings);

            // Should remain unchanged when invalid
            Assert.AreEqual(originalIndex, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithNegativeSizeIndex_DoesNotChangeSelection()
        {
            var options = new CliOptions { SizeIndex = -1 };
            var settings = CreateDefaultSettings();
            var originalIndex = settings.SelectedSizeIndex;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(originalIndex, settings.SelectedSizeIndex);
        }

        [TestMethod]
        public void Apply_WithUnspecifiedSimpleOptions_PreservesExistingSettings()
        {
            var options = new CliOptions();
            var settings = CreateDefaultSettings();
            settings.ShrinkOnly = true;
            settings.Replace = true;
            settings.IgnoreOrientation = false;
            settings.RemoveMetadata = true;
            settings.KeepDateModified = true;
            settings.JpegQualityLevel = 83;
            settings.FileName = "kept_%1";

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.ShrinkOnly);
            Assert.IsTrue(settings.Replace);
            Assert.IsFalse(settings.IgnoreOrientation);
            Assert.IsTrue(settings.RemoveMetadata);
            Assert.IsTrue(settings.KeepDateModified);
            Assert.AreEqual(83, settings.JpegQualityLevel);
            Assert.AreEqual("kept_%1", settings.FileName);
        }

        [TestMethod]
        public void Apply_WithExplicitFalseSimpleOptions_DisablesExistingOptions()
        {
            var options = new CliOptions
            {
                ShrinkOnly = false,
                Replace = false,
                IgnoreOrientation = false,
                RemoveMetadata = false,
                KeepDateModified = false,
            };
            var settings = CreateDefaultSettings();
            settings.ShrinkOnly = true;
            settings.Replace = true;
            settings.IgnoreOrientation = true;
            settings.RemoveMetadata = true;
            settings.KeepDateModified = true;

            CliSettingsApplier.Apply(options, settings);

            Assert.IsFalse(settings.ShrinkOnly);
            Assert.IsFalse(settings.Replace);
            Assert.IsFalse(settings.IgnoreOrientation);
            Assert.IsFalse(settings.RemoveMetadata);
            Assert.IsFalse(settings.KeepDateModified);
        }

        [TestMethod]
        public void Apply_WithShrinkOnly_SetsShrinkOnly()
        {
            var options = new CliOptions { ShrinkOnly = true };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.ShrinkOnly);
        }

        [TestMethod]
        public void Apply_WithReplace_SetsReplace()
        {
            var options = new CliOptions { Replace = true };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.Replace);
        }

        [TestMethod]
        public void Apply_WithIgnoreOrientation_SetsIgnoreOrientation()
        {
            var options = new CliOptions { IgnoreOrientation = true };
            var settings = CreateDefaultSettings();
            settings.IgnoreOrientation = false;

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.IgnoreOrientation);
        }

        [TestMethod]
        public void Apply_WithRemoveMetadata_SetsRemoveMetadata()
        {
            var options = new CliOptions { RemoveMetadata = true };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.RemoveMetadata);
        }

        [TestMethod]
        public void Apply_WithJpegQualityLevel_SetsJpegQualityLevel()
        {
            var options = new CliOptions { JpegQualityLevel = 85 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(85, settings.JpegQualityLevel);
        }

        [TestMethod]
        public void Apply_WithKeepDateModified_SetsKeepDateModified()
        {
            var options = new CliOptions { KeepDateModified = true };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.IsTrue(settings.KeepDateModified);
        }

        [TestMethod]
        public void Apply_WithFileName_SetsFileName()
        {
            var options = new CliOptions { FileName = "%1 (%2)" };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual("%1 (%2)", settings.FileName);
        }

        [TestMethod]
        public void Apply_WithEmptyFileName_DoesNotChangeFileName()
        {
            var options = new CliOptions { FileName = string.Empty };
            var settings = CreateDefaultSettings();
            settings.FileName = "kept_%1";
            var originalFileName = settings.FileName;

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(originalFileName, settings.FileName);
            Assert.AreEqual("kept_%1", settings.FileName);
        }

        [TestMethod]
        public void Apply_WithMultipleOptions_AppliesAllOptions()
        {
            var options = new CliOptions
            {
                Width = 800,
                Height = 600,
                Unit = ResizeUnit.Percent,
                Fit = ResizeFit.Fill,
                ShrinkOnly = true,
                Replace = true,
                IgnoreOrientation = true,
                RemoveMetadata = true,
                JpegQualityLevel = 90,
                KeepDateModified = true,
                FileName = "test_%2",
            };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(800.0, settings.CustomSize.Width);
            Assert.AreEqual(600.0, settings.CustomSize.Height);
            Assert.AreEqual(ResizeUnit.Percent, settings.CustomSize.Unit);
            Assert.AreEqual(ResizeFit.Fill, settings.CustomSize.Fit);
            Assert.IsTrue(settings.ShrinkOnly);
            Assert.IsTrue(settings.Replace);
            Assert.IsTrue(settings.IgnoreOrientation);
            Assert.IsTrue(settings.RemoveMetadata);
            Assert.AreEqual(90, settings.JpegQualityLevel);
            Assert.IsTrue(settings.KeepDateModified);
            Assert.AreEqual("test_%2", settings.FileName);
        }

        [TestMethod]
        public void Apply_CustomSizeTakesPrecedence_OverSizeIndex()
        {
            var options = new CliOptions
            {
                Width = 800,
                Height = 600,
                SizeIndex = 1, // Should be ignored when Width/Height specified
            };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            // Custom size should be selected, not preset
            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex);
            Assert.AreEqual(800.0, settings.CustomSize.Width);
        }

        [TestMethod]
        public void Apply_WithOnlyWidth_StillSelectsCustomSize()
        {
            var options = new CliOptions { Width = 800 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex);
            Assert.AreEqual(800.0, settings.CustomSize.Width);
        }

        [TestMethod]
        public void Apply_WithOnlyHeight_StillSelectsCustomSize()
        {
            var options = new CliOptions { Height = 600 };
            var settings = CreateDefaultSettings();

            CliSettingsApplier.Apply(options, settings);

            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex);
            Assert.AreEqual(600.0, settings.CustomSize.Height);
        }
    }
}
