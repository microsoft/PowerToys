// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class JumboShellIconTests
{
    [TestMethod]
    [DataRow(48, 64)]
    [DataRow(48, 96)]
    [DataRow(256, 64)]
    public async Task JumboShortcutIconsPreserveNativePaddingAndPixels(int iconSize, int requestedSize)
    {
        var iconPath = Path.Combine(Path.GetTempPath(), $"CmdPal-jumbo-{Guid.NewGuid():N}.ico");
        var shortcutPath = Path.ChangeExtension(iconPath, ".lnk");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            WriteIcon(iconPath, iconSize);
            shortcut.TargetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            shortcut.IconLocation = $"{iconPath},0";
            shortcut.Save();

            using var appIcon = await AppIconProtocolProcessor.Instance.PrepareAsync(
                AppIconProtocol.CreateJumbo(shortcutPath),
                requestedSize,
                ElementTheme.Default);
            using var prepared = appIcon.TakePreparedIcon();
            Assert.IsNotNull(prepared?.SoftwareBitmap);
            var bitmap = prepared.SoftwareBitmap;
            Assert.IsTrue(bitmap.PixelWidth >= requestedSize);
            Assert.AreEqual(bitmap.PixelWidth, bitmap.PixelHeight);

            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyToBuffer(pixels.AsBuffer());

            using var directIcon = ShellItemImageFactoryIconExtractor.Extract(iconPath, requestedSize);
            Assert.IsNotNull(directIcon);
            Assert.AreEqual(directIcon.PixelWidth, bitmap.PixelWidth);
            Assert.AreEqual(directIcon.PixelHeight, bitmap.PixelHeight);
            var directPixels = new byte[directIcon.PixelWidth * directIcon.PixelHeight * 4];
            directIcon.CopyToBuffer(directPixels.AsBuffer());
            CollectionAssert.AreEqual(directPixels, pixels, "Shortcut hero rendering must preserve the configured icon without adding overlays or changing its frame.");

            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                var alpha = pixels[offset + 3];
                Assert.IsTrue(pixels[offset] <= alpha && pixels[offset + 1] <= alpha && pixels[offset + 2] <= alpha, "WinUI requires premultiplied pixels, including the translucent Shell frame.");
            }

            if (iconSize == 48)
            {
                Assert.AreEqual(requestedSize, bitmap.PixelWidth);
                var padding = (requestedSize - iconSize) / 2;
                for (var y = 7; y < 36; y++)
                {
                    for (var x = 5; x < 36; x++)
                    {
                        var expected = x is >= 12 and < 20 && y is >= 14 and < 25 ? Color.Teal : Color.Crimson;
                        var offset = (((y + padding) * requestedSize) + x + padding) * 4;
                        Assert.AreEqual(expected.B, pixels[offset]);
                        Assert.AreEqual(expected.G, pixels[offset + 1]);
                        Assert.AreEqual(expected.R, pixels[offset + 2]);
                        Assert.AreEqual(expected.A, pixels[offset + 3]);
                    }
                }

                var translucentOffset = (((40 + padding) * requestedSize) + 40 + padding) * 4;
                Assert.AreEqual(25, pixels[translucentOffset], 1);
                Assert.AreEqual(50, pixels[translucentOffset + 1], 1);
                Assert.AreEqual(100, pixels[translucentOffset + 2], 1);
                Assert.AreEqual(128, pixels[translucentOffset + 3]);
            }
            else
            {
                // Native 256-pixel artwork with intentional padding must not be cropped or enlarged.
                var center = ((bitmap.PixelHeight / 2 * bitmap.PixelWidth) + (bitmap.PixelWidth / 2)) * 4;
                Assert.AreEqual(0, pixels[center + 3]);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
            File.Delete(iconPath);
        }
    }

    private static void WriteIcon(string path, int size)
    {
        using var bitmap = new Bitmap(size, size);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            // Even genuine 256-pixel artwork may occupy only the top-left corner.
            graphics.Clear(Color.Transparent);
            graphics.FillRectangle(Brushes.Crimson, 5, 7, 31, 29);
            graphics.FillRectangle(Brushes.Teal, 12, 14, 8, 11);
            using var translucentBrush = new SolidBrush(Color.FromArgb(128, 200, 100, 50));
            graphics.FillRectangle(translucentBrush, 38, 38, 6, 6);
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)png.Length);
        writer.Write(22u);
        writer.Write(png.ToArray());
    }
}
