// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace AdvancedPaste.UITests;

internal static class ClipboardFixtures
{
    internal const string OcrText = "POWERTOYS OFFLINE 123";
    internal const int ImageWidth = 640;
    internal const int ImageHeight = 160;

    internal static void CreateImage(string path, bool withText = false)
    {
        using var bitmap = new Bitmap(ImageWidth, ImageHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        if (withText)
        {
            graphics.Clear(Color.White);
            using var font = new Font("Segoe UI", 28, FontStyle.Regular);
            graphics.DrawString(OcrText, font, Brushes.Black, 20, 50);
        }
        else
        {
            graphics.Clear(Color.CornflowerBlue);
            graphics.FillRectangle(Brushes.Gold, ImageWidth / 2, 0, ImageWidth / 2, ImageHeight);
        }

        bitmap.Save(path, ImageFormat.Png);
        Assert.IsTrue(File.Exists(path) && new FileInfo(path).Length > 0, "The image fixture was not written.");
    }

    internal static void CreateWave(string path)
    {
        const int sampleRate = 44_100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int byteRate = sampleRate * channels * bitsPerSample / 8;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + byteRate);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write("data"u8);
            writer.Write(byteRate);
            for (var sample = 0; sample < sampleRate; sample++)
            {
                writer.Write((short)(8_000 * Math.Sin(2 * Math.PI * 440 * sample / sampleRate)));
            }
        }

        Assert.AreEqual(44 + byteRate, new FileInfo(path).Length, "The PCM audio fixture has the wrong size.");
    }

    internal static async Task CreateVideoAsync(string path)
    {
        var audioPath = Path.ChangeExtension(path, ".wav");
        CreateWave(audioPath);
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 100, 149, 237), TimeSpan.FromSeconds(1)));
        composition.BackgroundAudioTracks.Add(await BackgroundAudioTrack.CreateFromFileAsync(await StorageFile.GetFileFromPathAsync(audioPath)));
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
        profile.Video.Width = 320;
        profile.Video.Height = 180;
        profile.Video.FrameRate.Numerator = 15;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = 256_000;
        profile.Audio.Bitrate = 128_000;
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        var result = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
        Assert.AreEqual(TranscodeFailureReason.None, result, "Windows could not render the offline video fixture.");
        var actual = await MediaEncodingProfile.CreateFromFileAsync(file);
        Assert.IsNotNull(actual.Audio, "The video fixture has no audio track.");
        Assert.IsNotNull(actual.Video, "The video fixture has no video track.");
        Assert.AreEqual(320U, actual.Video.Width, "The video fixture has the wrong dimensions.");
    }
}
