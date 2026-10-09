// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ImageResizer.Models;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Cli
{
    [TestClass]
    public class ImageResizerCliExecutorTests
    {
        [TestMethod]
        public void GetShrinkOnlyPercentWarning_ReturnsWarningForEffectivePercentSize()
        {
            var settings = SettingsWithSize(ResizeUnit.Percent);
            settings.ShrinkOnly = true;

            var warning = ImageResizerCliExecutor.GetShrinkOnlyPercentWarning(settings);

            Assert.AreEqual(Resources.CLI_WarningShrinkOnlyPercent, warning);
        }

        [TestMethod]
        public void GetShrinkOnlyPercentWarning_ReturnsNullForPixelSize()
        {
            var settings = SettingsWithSize(ResizeUnit.Pixel);
            settings.ShrinkOnly = true;

            var warning = ImageResizerCliExecutor.GetShrinkOnlyPercentWarning(settings);

            Assert.IsNull(warning);
        }

        [TestMethod]
        public void GetShrinkOnlyPercentWarning_ReturnsNullWhenShrinkOnlyIsDisabled()
        {
            var settings = SettingsWithSize(ResizeUnit.Percent);
            settings.ShrinkOnly = false;

            var warning = ImageResizerCliExecutor.GetShrinkOnlyPercentWarning(settings);

            Assert.IsNull(warning);
        }

        [TestMethod]
        public void GetEffectiveSizeValidationError_RejectsHeightOnlyPercentFit()
        {
            var options = new CliOptions { Height = 50, Unit = ResizeUnit.Percent, Fit = ResizeFit.Fit };
            var settings = SettingsWithSize(ResizeUnit.Pixel);
            CliSettingsApplier.Apply(options, settings);

            var error = ImageResizerCliExecutor.GetEffectiveSizeValidationError(options, settings);

            Assert.AreEqual(Resources.CLI_ErrorPercentWidthRequired, error);
        }

        [TestMethod]
        public void GetEffectiveSizeValidationError_AllowsHeightOnlyPercentStretch()
        {
            var options = new CliOptions { Height = 50, Unit = ResizeUnit.Percent, Fit = ResizeFit.Stretch };
            var settings = SettingsWithSize(ResizeUnit.Pixel);
            CliSettingsApplier.Apply(options, settings);

            var error = ImageResizerCliExecutor.GetEffectiveSizeValidationError(options, settings);

            Assert.IsNull(error);
        }

        [TestMethod]
        public void GetEffectiveSizeValidationError_AllowsPositiveWidthPercentFit()
        {
            var options = new CliOptions { Width = 50, Unit = ResizeUnit.Percent, Fit = ResizeFit.Fit };
            var settings = SettingsWithSize(ResizeUnit.Pixel);
            CliSettingsApplier.Apply(options, settings);

            var error = ImageResizerCliExecutor.GetEffectiveSizeValidationError(options, settings);

            Assert.IsNull(error);
        }

        [TestMethod]
        public void GetSizeIndexValidationError_RejectsOutOfRangePreset()
        {
            var options = new CliOptions { SizeIndex = 999 };
            var settings = SettingsWithSize(ResizeUnit.Pixel);

            var error = ImageResizerCliExecutor.GetSizeIndexValidationError(options, settings);

            StringAssert.Contains(error, "999");
        }

        [TestMethod]
        public void GetSizeIndexValidationError_AllowsExistingPreset()
        {
            var options = new CliOptions { SizeIndex = 0 };
            var settings = SettingsWithSize(ResizeUnit.Pixel);

            var error = ImageResizerCliExecutor.GetSizeIndexValidationError(options, settings);

            Assert.IsNull(error);
        }

        [TestMethod]
        public void Run_WithOutOfRangePresetAndReplace_DoesNotModifySource()
        {
            using var directory = new TestDirectory();
            var testDirectory = Path.GetDirectoryName(typeof(ImageResizerCliExecutorTests).Assembly.Location);
            var source = Path.Combine(directory, "source.png");
            File.Copy(Path.Combine(testDirectory, "Test.png"), source);
            var hashBefore = File.ReadAllBytes(source);

            var exitCode = new ImageResizerCliExecutor().Run(["--size", "999", "--replace", source]);

            Assert.AreEqual(1, exitCode);
            CollectionAssert.AreEqual(hashBefore, File.ReadAllBytes(source));
            Assert.AreEqual(1, directory.FileNames.Count());
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task Run_WithEmptyNamedPipe_ReturnsError()
        {
            var pipeName = $"ImageResizer-{Guid.NewGuid():N}";
            using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var writeTask = CompleteEmptyPipeAsync(pipe);
            var executor = new ImageResizerCliExecutor();

            var exitCode = executor.Run([$@"\\.\pipe\{pipeName}"]);
            await writeTask;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual("error", executor.CommandName);
        }

        private static Settings SettingsWithSize(ResizeUnit unit)
        {
            var settings = new Settings();
            settings.CustomSize.Unit = unit;
            settings.SelectedSizeIndex = settings.Sizes.Count;
            return settings;
        }

        private static async Task CompleteEmptyPipeAsync(NamedPipeServerStream pipe)
        {
            await pipe.WaitForConnectionAsync().ConfigureAwait(false);
            using var writer = new StreamWriter(pipe, Encoding.Unicode);
        }
    }
}
