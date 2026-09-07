// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class PreviewSnapshot : IDisposable
    {
        private readonly Bitmap bitmap;

        private PreviewSnapshot(Bitmap bitmap)
        {
            this.bitmap = bitmap;
        }

        internal static PreviewSnapshot Capture(TestContext context, Session editor, string label)
        {
            MouseHelper.MoveTo(0, 0);
            var image = editor.Find<Element>(By.AccessibilityId("WorkspacePreviewImage"), 15_000);
            Assert.IsTrue(image.Width > 0 && image.Height > 0, "The visible workspace preview has no geometry.");
            var directory = context.TestRunResultsDirectory ?? throw new InvalidOperationException("No results directory is available.");
            var framePath = Path.Combine(directory, $"{context.TestName}-{Guid.NewGuid():N}-frame.png");
            var previewPath = Path.Combine(directory, $"{context.TestName}-{Guid.NewGuid():N}-{label}.png");
            Directory.CreateDirectory(directory);
            try
            {
                editor.ScreenshotVisibleWindow(framePath);
                var bounds = WindowHelper.GetVisibleBounds(new IntPtr(editor.WindowHandle));
                var rectangle = new Rectangle(image.X - bounds.Left, image.Y - bounds.Top, image.Width, image.Height);
                using var frame = new Bitmap(framePath);
                Assert.IsTrue(new Rectangle(0, 0, frame.Width, frame.Height).Contains(rectangle), "The preview is not entirely within the composed editor frame.");
                var cropped = frame.Clone(rectangle, PixelFormat.Format32bppArgb);
                cropped.Save(previewPath, ImageFormat.Png);
                context.AddResultFile(previewPath);
                return new PreviewSnapshot(cropped);
            }
            finally
            {
                File.Delete(framePath);
            }
        }

        internal static void AssertChanged(TestContext context, Session editor, PreviewSnapshot before) =>
            AssertDifference(context, editor, before, changed: true);

        internal static void AssertRestored(TestContext context, Session editor, PreviewSnapshot before) =>
            AssertDifference(context, editor, before, changed: false);

        public void Dispose() => bitmap.Dispose();

        private static void AssertDifference(TestContext context, Session editor, PreviewSnapshot before, bool changed)
        {
            var result = WaitHelper.WaitForStable(
                () =>
                {
                    using var after = Capture(context, editor, changed ? "changed-preview" : "restored-preview");
                    return before.ChangedFraction(after);
                },
                fraction => changed ? fraction >= 0.005 : fraction <= 0.001,
                timeoutMS: 20_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 150);
            Assert.IsTrue(
                result.Succeeded,
                $"The preview {(changed ? "did not materially change" : "did not return to its original pixels")}. Changed fraction: {result.LastObservation:P3}.");
        }

        private double ChangedFraction(PreviewSnapshot other)
        {
            var otherBitmap = other.bitmap;
            Assert.AreEqual(bitmap.Size, otherBitmap.Size, "Preview dimensions changed during an application edit.");
            var first = Pixels(bitmap);
            var second = Pixels(otherBitmap);
            var changed = 0;
            for (var offset = 0; offset < first.Length; offset += 4)
            {
                var difference = Math.Abs(first[offset] - second[offset]) +
                    Math.Abs(first[offset + 1] - second[offset + 1]) +
                    Math.Abs(first[offset + 2] - second[offset + 2]);
                if (difference > 48)
                {
                    changed++;
                }
            }

            return changed / (double)(bitmap.Width * bitmap.Height);
        }

        private static byte[] Pixels(Bitmap image)
        {
            var pixels = new byte[checked(image.Width * image.Height * 4)];
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var row = 0; row < image.Height; row++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * image.Width * 4, image.Width * 4);
                }
            }
            finally
            {
                image.UnlockBits(data);
            }

            return pixels;
        }
    }
}
