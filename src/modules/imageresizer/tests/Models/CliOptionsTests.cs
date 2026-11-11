// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ImageResizer.Cli.Commands;
using ImageResizer.Models;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Tests.Models
{
    [OSCondition(OperatingSystems.Windows)]
    [TestClass]
    public class CliOptionsTests
    {
        private static readonly string[] _multiFileArgs = new[] { "test1.jpg", "test2.jpg", "test3.jpg" };
        private static readonly string[] _mixedOptionsArgs = new[] { "--width", "800", "test1.jpg", "--height", "600", "test2.jpg" };
        private static readonly Lazy<CompositeFormat> _invalidEnumValueFormat = new(() => CompositeFormat.Parse(Resources.CLI_ErrorInvalidEnumValue));

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
        [DataRow("--unit", "Centimeter", ResizeUnit.Centimeter)]
        [DataRow("--unit", "Inch", ResizeUnit.Inch)]
        [DataRow("--unit", "Percent", ResizeUnit.Percent)]
        [DataRow("--unit", "Pixel", ResizeUnit.Pixel)]
        [DataRow("-u", "piXEl", ResizeUnit.Pixel)]
        [DataRow("--unit", "0", ResizeUnit.Centimeter)]
        [DataRow("--unit", "1", ResizeUnit.Inch)]
        [DataRow("--unit", "2", ResizeUnit.Percent)]
        [DataRow("--unit", "3", ResizeUnit.Pixel)]
        [DataRow("-u", "3", ResizeUnit.Pixel)]
        public void Parse_WithValidUnit_SetsUnit(string option, string value, ResizeUnit expected)
        {
            var options = CliOptions.ParseForCli([option, value, "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual(expected, options.Unit);
        }

        [TestMethod]
        [DataRow("--fit", "Fill", ResizeFit.Fill)]
        [DataRow("--fit", "Fit", ResizeFit.Fit)]
        [DataRow("--fit", "Stretch", ResizeFit.Stretch)]
        [DataRow("-f", "fIt", ResizeFit.Fit)]
        [DataRow("--fit", "0", ResizeFit.Fill)]
        [DataRow("--fit", "1", ResizeFit.Fit)]
        [DataRow("--fit", "2", ResizeFit.Stretch)]
        [DataRow("-f", "2", ResizeFit.Stretch)]
        public void Parse_WithValidFit_SetsFit(string option, string value, ResizeFit expected)
        {
            var options = CliOptions.ParseForCli([option, value, "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual(expected, options.Fit);
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

            Assert.IsTrue(options.ShrinkOnly);
        }

        [TestMethod]
        public void Parse_WithReplace_SetsReplace()
        {
            var args = new[] { "--replace", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.Replace);
        }

        [TestMethod]
        public void Parse_WithIgnoreOrientation_SetsIgnoreOrientation()
        {
            var args = new[] { "--ignore-orientation", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.IgnoreOrientation);
        }

        [TestMethod]
        public void Parse_WithRemoveMetadata_SetsRemoveMetadata()
        {
            var args = new[] { "--remove-metadata", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.RemoveMetadata);
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

            Assert.IsTrue(options.KeepDateModified);
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

            Assert.IsTrue(options.ProgressLines);
        }

        [TestMethod]
        public void Parse_WithAccessibleAlias_SetsProgressLines()
        {
            var args = new[] { "--accessible", "test.jpg" };
            var options = CliOptions.Parse(args);

            Assert.IsTrue(options.ProgressLines);
        }

        [TestMethod]
        public void Parse_WithMultipleFiles_AddsAllFiles()
        {
            var args = _multiFileArgs;
            var options = CliOptions.Parse(args);

            Assert.HasCount(3, options.Files);
            Assert.Contains("test1.jpg", options.Files.ToList());
            Assert.Contains("test2.jpg", options.Files.ToList());
            Assert.Contains("test3.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithMixedOptionsAndFiles_ParsesCorrectly()
        {
            var args = _mixedOptionsArgs;
            var options = CliOptions.Parse(args);

            Assert.AreEqual(800.0, options.Width);
            Assert.AreEqual(600.0, options.Height);
            Assert.HasCount(2, options.Files);
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
            Assert.IsEmpty(options.Files);
            Assert.IsNull(options.Unit);
            Assert.IsNull(options.Fit);
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

        [TestMethod]
        [DataRow("--unit", "Bogus")]
        [DataRow("--fit", "Bogus")]
        [DataRow("--unit", "99")]
        [DataRow("--fit", "99")]
        [DataRow("--unit", "4")]
        [DataRow("--fit", "3")]
        [DataRow("--unit", "-1")]
        [DataRow("--fit", "-1")]
        [DataRow("--unit", "2147483647")]
        [DataRow("--fit", "2147483647")]
        [DataRow("--unit", "-2147483648")]
        [DataRow("--fit", "-2147483648")]
        [DataRow("--unit", "2147483648")]
        [DataRow("--fit", "2147483648")]
        [DataRow("--unit", "-2147483649")]
        [DataRow("--fit", "-2147483649")]
        public void Parse_WithInvalidEnumValue_ReturnsError(string option, string value)
        {
            var options = CliOptions.ParseForCli([option, value, "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains(option, options.ParseErrors[0]);
            Assert.Contains(value, options.ParseErrors[0]);
            Assert.IsNull(options.Unit);
            Assert.IsNull(options.Fit);
        }

        [TestMethod]
        [DataRow("-u", "--unit")]
        [DataRow("-f", "--fit")]
        public void Parse_WithUndefinedEnumAndShortAlias_ReturnsLocalizedError(string alias, string option)
        {
            var options = CliOptions.ParseForCli([alias, "99", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, _invalidEnumValueFormat.Value, "99", option),
                options.ParseErrors[0]);
            Assert.IsNull(options.Unit);
            Assert.IsNull(options.Fit);
        }

        [TestMethod]
        public void Parse_WithAttachedDefinedEnumValues_PreservesCompatibility()
        {
            var options = CliOptions.ParseForCli(["-u3", "-f2", "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual(ResizeUnit.Pixel, options.Unit);
            Assert.AreEqual(ResizeFit.Stretch, options.Fit);
        }

        [TestMethod]
        public void Parse_WithInvalidUnit_PreservesValidFitAndInput()
        {
            var options = CliOptions.ParseForCli(["--unit", "99", "--fit", "Stretch", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.IsNull(options.Unit);
            Assert.AreEqual(ResizeFit.Stretch, options.Fit);
            Assert.Contains("test.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithInvalidUnitAndFit_ReturnsBothErrors()
        {
            var options = CliOptions.ParseForCli(["--unit", "99", "--fit", "99", "test.jpg"]);

            Assert.HasCount(2, options.ParseErrors);
            Assert.IsNull(options.Unit);
            Assert.IsNull(options.Fit);
        }

        [TestMethod]
        [DataRow("--size", "abc")]
        [DataRow("--quality", "abc")]
        [DataRow("-q", "abc")]
        [DataRow("--quality", "99999999999")]
        public void Parse_WithUnparsableIntegerValue_ReturnsParseError(string option, string value)
        {
            var options = CliOptions.ParseForCli([option, value, "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains(value, options.ParseErrors[0]);
            Assert.IsNull(options.SizeIndex);
            Assert.IsNull(options.JpegQualityLevel);
        }

        [TestMethod]
        [DataRow("--size", "-1")]
        [DataRow("--quality", "0")]
        [DataRow("--quality", "101")]
        public void Parse_WithOutOfRangeIntegerValue_ReturnsValidationError(string option, string value)
        {
            var options = CliOptions.ParseForCli([option, value, "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.IsNull(options.SizeIndex);
            Assert.IsNull(options.JpegQualityLevel);
        }

        [TestMethod]
        [DataRow("[suggest]", "abc")]
        [DataRow("[suggest:30]", "abc")]
        [DataRow("[suggest]", "-1")]
        [DataRow("[suggest:30]", "-1")]
        public void Parse_WithCompletionDirectiveAndInvalidWidth_ReturnsError(string directive, string width)
        {
            var options = CliOptions.ParseForCli([directive, "--width", width, "--replace", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.IsNull(options.Width);
        }

        [TestMethod]
        [DataRow("[suggest]")]
        [DataRow("[suggest:30]")]
        public void Parse_WithCompletionDirectiveAndMissingResponseFile_ReturnsError(string directive)
        {
            using var directory = new TestDirectory();
            var responseFile = Path.Combine(directory, "missing.rsp");

            var options = CliOptions.ParseForCli([directive, "@" + responseFile, "--replace", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains("missing.rsp", options.ParseErrors[0]);
        }

        [TestMethod]
        public void Parse_WithUnknownLongOption_ReturnsError()
        {
            var options = CliOptions.ParseForCli(["--bogus", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains("--bogus", options.ParseErrors[0]);
        }

        [TestMethod]
        public void Parse_WithUnknownOptionAfterFile_ReturnsError()
        {
            var options = CliOptions.ParseForCli(["test.jpg", "--bogus"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains("--bogus", options.ParseErrors[0]);
        }

        [TestMethod]
        public void Parse_WithRootRelativeSlashPath_AllowsFile()
        {
            var options = CliOptions.ParseForCli(["/bogus", "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.Contains("/bogus", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithPrefixedDashFileName_AllowsFile()
        {
            var options = CliOptions.ParseForCli([@".\-photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.Contains(@".\-photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithEndOfOptions_AllowsOptionLikeFileName()
        {
            var options = CliOptions.ParseForCli(["--", "--photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.Contains("--photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_LenientModePreservesOptionLikeFileForGui()
        {
            var options = CliOptions.Parse(["--photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.Contains("--photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithUnknownBundledOption_ReturnsError()
        {
            var options = CliOptions.ParseForCli(["-replace", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains("-replace", options.ParseErrors[0]);
        }

        [TestMethod]
        public void Parse_WithBundledValueOption_PreservesCompatibility()
        {
            var options = CliOptions.ParseForCli(["-w100", "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual(100.0, options.Width);
        }

        [TestMethod]
        public void Parse_WithBundledFlagAndValueOption_PreservesCompatibility()
        {
            var options = CliOptions.ParseForCli(["-rq85", "test.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.IsTrue(options.Replace);
            Assert.AreEqual(85, options.JpegQualityLevel);
        }

        [TestMethod]
        public void Parse_WithInvalidBundleInResponseFile_ReturnsError()
        {
            var responseFile = Path.Combine(Path.GetTempPath(), $"ImageResizer-{Guid.NewGuid():N}.rsp");
            try
            {
                File.WriteAllLines(responseFile, ["-rphoto.jpg", "photo.jpg"]);

                var options = CliOptions.ParseForCli(["@" + responseFile]);

                Assert.HasCount(1, options.ParseErrors);
                Assert.Contains("-rphoto.jpg", options.ParseErrors[0]);
            }
            finally
            {
                File.Delete(responseFile);
            }
        }

        [TestMethod]
        public void Parse_WithEndOfOptionsInResponseFile_AllowsOptionLikeFiles()
        {
            var responseFile = Path.Combine(Path.GetTempPath(), $"ImageResizer-{Guid.NewGuid():N}.rsp");
            try
            {
                File.WriteAllLines(responseFile, ["--"]);

                var options = CliOptions.ParseForCli(["@" + responseFile, "-rphoto.jpg", "photo.jpg"]);

                Assert.IsEmpty(options.ParseErrors);
                Assert.Contains("-rphoto.jpg", options.Files.ToList());
                Assert.Contains("photo.jpg", options.Files.ToList());
            }
            finally
            {
                File.Delete(responseFile);
            }
        }

        [TestMethod]
        public void Parse_WithConversionError_PreservesFilesAndPipeForLenientCallers()
        {
            var options = CliOptions.Parse(["--width", "abc", "good.jpg", @"\\.\pipe\ImageResizerTest"]);

            Assert.IsNotEmpty(options.ParseErrors);
            Assert.Contains("good.jpg", options.Files.ToList());
            Assert.AreEqual("ImageResizerTest", options.PipeName);
        }

        [TestMethod]
        public void Parse_WithConversionError_PreservesValidDestinationForLenientCallers()
        {
            var options = CliOptions.Parse(["/d", @"C:\Output", "--width", "bad", "good.jpg"]);

            Assert.IsNotEmpty(options.ParseErrors);
            Assert.AreEqual(@"C:\Output", options.DestinationDirectory);
            Assert.Contains("good.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithOptionLikeFilenameValue_DoesNotReportBundleError()
        {
            var options = CliOptions.ParseForCli(["--filename", "-rphoto.jpg", "photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual("-rphoto.jpg", options.FileName);
        }

        [TestMethod]
        public void Parse_WithEmptyLongOptionSeparator_ConsumesFollowingOptionLikeValue()
        {
            var options = CliOptions.ParseForCli(["--filename=", "-rphoto.jpg", "photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual("-rphoto.jpg", options.FileName);
            Assert.Contains("photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithEmptyShortOptionSeparator_ConsumesFollowingOptionLikeValue()
        {
            var options = CliOptions.ParseForCli(["-n=", "-rphoto.jpg", "photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.AreEqual("-rphoto.jpg", options.FileName);
            Assert.Contains("photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithExplicitBundledBooleanValue_PreservesCompatibility()
        {
            var options = CliOptions.ParseForCli(["-r=false", "photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.IsNull(options.Replace);
            Assert.Contains("photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        [DataRow("-rtrue")]
        [DataRow("-rTRUE")]
        public void Parse_WithAttachedTrueBooleanValue_PreservesCompatibility(string argument)
        {
            var options = CliOptions.ParseForCli([argument, "-q85", "photo.jpg"]);

            Assert.IsEmpty(options.ParseErrors);
            Assert.IsTrue(options.Replace);
            Assert.AreEqual(85, options.JpegQualityLevel);
            Assert.Contains("photo.jpg", options.Files.ToList());
        }

        [TestMethod]
        public void Parse_WithBundledEmptyValue_DoesNotSkipFollowingInvalidBundle()
        {
            var options = CliOptions.ParseForCli(["-rn=", "-rphoto.jpg", "photo.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
            Assert.Contains("-rphoto.jpg", options.ParseErrors[0]);
        }

        [TestMethod]
        public void Parse_WithNegativeWidth_ReturnsError()
        {
            var options = CliOptions.Parse(["--width", "-100", "--height", "100", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
        }

        [TestMethod]
        [DataRow("NaN")]
        [DataRow("Infinity")]
        [DataRow("-Infinity")]
        public void Parse_WithNonFiniteDimension_ReturnsError(string value)
        {
            var options = CliOptions.Parse(["--width", value, "--height", "100", "test.jpg"]);

            Assert.IsNotEmpty(options.ParseErrors);
        }

        [TestMethod]
        public void Parse_WithBothCustomDimensionsZero_ReturnsError()
        {
            var options = CliOptions.Parse(["--width", "0", "--height", "0", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
        }

        [TestMethod]
        public void Parse_WithOnlyZeroWidth_ReturnsError()
        {
            var options = CliOptions.Parse(["--width", "0", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
        }

        [TestMethod]
        public void Parse_WithDimensionAboveInt32Range_ReturnsError()
        {
            var options = CliOptions.Parse(["--width", "2147483648", "--height", "100", "test.jpg"]);

            Assert.HasCount(1, options.ParseErrors);
        }

        [TestMethod]
        [DataRow("en-US")]
        [DataRow("en-HK")]
        public void Parse_WithGroupedDimensionAboveInt32Range_ReturnsError(string cultureName)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

                var options = CliOptions.Parse(["--width", "2,147,483,648", "--height", "100", "test.jpg"]);

                Assert.HasCount(1, options.ParseErrors);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
