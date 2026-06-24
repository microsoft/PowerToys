// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using ImageResizer.Cli.Commands;
using ImageResizer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Tests.Models
{
    [TestClass]
    public class CliOptionsTests
    {
        private static readonly string[] _multiFileArgs = new[] { "test1.jpg", "test2.jpg", "test3.jpg" };
        private static readonly string[] _mixedOptionsArgs = new[] { "--width", "800", "test1.jpg", "--height", "600", "test2.jpg" };

        [TestMethod]
        public void Parse_WithValidWidth_SetsWidth()
        {
            var args = new[] { "--width", "800", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(800.0, options.Width);
        }

        [TestMethod]
        public void Parse_WithValidHeight_SetsHeight()
        {
            var args = new[] { "--height", "600", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(600.0, options.Height);
        }

        [TestMethod]
        public void Parse_WithShortWidthAlias_WorksIdentically()
        {
            var longFormArgs = new[] { "--width", "800", "test.jpg" };
            var shortFormArgs = new[] { "-w", "800", "test.jpg" };
            var longForm = CliOptions.Parse(longFormArgs);
            var shortForm = CliOptions.Parse(shortFormArgs);

            Assert.AreEqual(longForm.Width, shortForm.Width);
        }

        [TestMethod]
        public void Parse_WithShortHeightAlias_WorksIdentically()
        {
            var longFormArgs = new[] { "--height", "600", "test.jpg" };
            var shortFormArgs = new[] { "-h", "600", "test.jpg" };
            var longForm = CliOptions.Parse(longFormArgs);
            var shortForm = CliOptions.Parse(shortFormArgs);

            Assert.AreEqual(longForm.Height, shortForm.Height);
        }

        [TestMethod]
        public void Parse_WithValidUnit_SetsUnit()
        {
            var args = new[] { "--unit", "Percent", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(ResizeUnit.Percent, options.Unit);
        }

        [TestMethod]
        public void Parse_WithValidFit_SetsFit()
        {
            var args = new[] { "--fit", "Fill", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(ResizeFit.Fill, options.Fit);
        }

        [TestMethod]
        public void Parse_WithSizeIndex_SetsSizeIndex()
        {
            var args = new[] { "--size", "2", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(2, options.SizeIndex);
        }

        [TestMethod]
        public void Parse_WithShrinkOnly_SetsShrinkOnly()
        {
            var args = new[] { "--shrink-only", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.ShrinkOnly);
        }

        [TestMethod]
        public void Parse_WithReplace_SetsReplace()
        {
            var args = new[] { "--replace", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.Replace);
        }

        [TestMethod]
        public void Parse_WithIgnoreOrientation_SetsIgnoreOrientation()
        {
            var args = new[] { "--ignore-orientation", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.IgnoreOrientation);
        }

        [TestMethod]
        public void Parse_WithRemoveMetadata_SetsRemoveMetadata()
        {
            var args = new[] { "--remove-metadata", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.RemoveMetadata);
        }

        [TestMethod]
        public void Parse_WithValidQuality_SetsQuality()
        {
            var args = new[] { "--quality", "85", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(85, options.JpegQualityLevel);
        }

        [TestMethod]
        public void Parse_WithKeepDateModified_SetsKeepDateModified()
        {
            var args = new[] { "--keep-date-modified", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.KeepDateModified);
        }

        [TestMethod]
        public void Parse_WithFileName_SetsFileName()
        {
            var args = new[] { "--filename", "%1 (%2)", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual("%1 (%2)", options.FileName);
        }

        [TestMethod]
        public void Parse_WithDestination_SetsDestinationDirectory()
        {
            var args = new[] { "--destination", "C:\\Output", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual("C:\\Output", options.DestinationDirectory);
        }

        [TestMethod]
        public void Parse_WithShortDestinationAlias_WorksIdentically()
        {
            var longFormArgs = new[] { "--destination", "C:\\Output", "test.jpg" };
            var shortFormArgs = new[] { "-d", "C:\\Output", "test.jpg" };
            var longForm = CliOptions.Parse(longFormArgs);
            var shortForm = CliOptions.Parse(shortFormArgs);

            Assert.AreEqual(longForm.DestinationDirectory, shortForm.DestinationDirectory);
        }

        [TestMethod]
        public void Parse_WithProgressLines_SetsProgressLines()
        {
            var args = new[] { "--progress-lines", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.ProgressLines);
        }

        [TestMethod]
        public void Parse_WithAccessibleAlias_SetsProgressLines()
        {
            var args = new[] { "--accessible", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(true, options.ProgressLines);
        }

        [TestMethod]
        public void Parse_WithMultipleFiles_AddsAllFiles()
        {
            var args = _multiFileArgs;
            var options = CliOptions.Parse(args);

            Assert.AreEqual(3, options.Files.Count);
            CollectionAssert.Contains(options.Files.ToList(), "test1.jpg");
            CollectionAssert.Contains(options.Files.ToList(), "test2.jpg");
            CollectionAssert.Contains(options.Files.ToList(), "test3.jpg");
        }

        [TestMethod]
        public void Parse_WithMixedOptionsAndFiles_ParsesCorrectly()
        {
            var args = _mixedOptionsArgs;
            var options = CliOptions.Parse(args);

            Assert.AreEqual(800.0, options.Width);
            Assert.AreEqual(600.0, options.Height);
            Assert.AreEqual(2, options.Files.Count);
        }

        [TestMethod]
        public void Parse_WithHelp_SetsShowHelp()
        {
            var args = new[] { "--help" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.ShowHelp);
        }

        [TestMethod]
        public void Parse_WithShowConfig_SetsShowConfig()
        {
            var args = new[] { "--show-config" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.ShowConfig);
        }

        [TestMethod]
        public void Parse_WithNoArguments_ReturnsEmptyOptions()
        {
            var args = Array.Empty<string>();
            var options = CliOptions.Parse(args);

            Assert.IsNotNull(options);
            Assert.AreEqual(0, options.Files.Count);
        }

        [TestMethod]
        public void Parse_WithZeroWidth_AllowsZeroValue()
        {
            var args = new[] { "--width", "0", "--height", "600", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(0.0, options.Width);
            Assert.AreEqual(600.0, options.Height);
        }

        [TestMethod]
        public void Parse_WithZeroHeight_AllowsZeroValue()
        {
            var args = new[] { "--width", "800", "--height", "0", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(800.0, options.Width);
            Assert.AreEqual(0.0, options.Height);
        }

        [TestMethod]
        public void Parse_CaseInsensitiveEnums_ParsesCorrectly()
        {
            var args = new[] { "--unit", "pixel", "--fit", "fit", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.AreEqual(ResizeUnit.Pixel, options.Unit);
            Assert.AreEqual(ResizeFit.Fit, options.Fit);
        }
    }
}
