// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
public sealed class ZoomItCaptureHelpersTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(638, 358)]
    [DataRow(322, 242)]
    public async Task DecodedVideoFramesRetainNativeDimensions(int width, int height)
    {
        var directory = Path.Combine(TestContext.TestRunDirectory!, nameof(ZoomItCaptureHelpersTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = new MediaComposition();
            source.Clips.Add(MediaClip.CreateFromColor(new Windows.UI.Color { A = 255, B = 255 }, TimeSpan.FromSeconds(1)));
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            profile.Video.Width = (uint)width;
            profile.Video.Height = (uint)height;
            var folder = await StorageFolder.GetFolderFromPathAsync(directory).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            var file = await folder.CreateFileAsync("native-dimensions.mp4").AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            var result = await source.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(45));
            Assert.AreEqual(TranscodeFailureReason.None, result, "Could not create the synthetic video fixture.");

            var clip = await MediaClip.CreateFromFileAsync(file).AsTask().WaitAsync(TimeSpan.FromSeconds(45));
            var encoding = clip.GetVideoEncodingProperties();
            Assert.AreEqual((uint)width, encoding.Width);
            Assert.AreEqual((uint)height, encoding.Height);
            var composition = new MediaComposition();
            composition.Clips.Add(clip);
            using var frame = await ZoomItCaptureHelpers.DecodeVideoFrameAsync(composition, TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(new Size(width, height), frame.Size, "Decoding must not scale the source to a requested thumbnail size.");
            var pixel = frame.GetPixel(width / 2, height / 2);
            Assert.IsTrue(pixel.B > 230 && pixel.R < 25 && pixel.G < 25, $"Expected the synthetic blue frame, got {pixel}.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
