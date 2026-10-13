// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.FilePreviewer.Models;
using Windows.Foundation;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class ImagePreviewBufferTests
    {
        private static ImageSource CreateSource() =>
            (ImageSource)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));

        [TestMethod]
        [DataRow(1.0)]
        [DataRow(1.5)]
        [DataRow(2.0)]
        public void Prepare_SmallImage_PreservesVisibleFrameBoundsUntilSwap(double scalingFactor)
        {
            var largeSource = CreateSource();
            var smallSource = CreateSource();
            var largeFrame = new ImagePreviewFrame(largeSource, new Size(800, 600));
            var smallFrame = new ImagePreviewFrame(smallSource, new Size(32, 32));
            var buffer = new ImagePreviewBuffer();
            buffer.Prepare(largeFrame);
            Assert.IsTrue(buffer.Swap());

            buffer.Prepare(smallFrame);

            Assert.IsNotNull(buffer.Current);
            Assert.IsNotNull(buffer.Next);
            Assert.AreSame(largeFrame, buffer.Current);
            Assert.AreSame(smallFrame, buffer.Next);
            Assert.AreSame(largeSource, buffer.Current.Source);
            Assert.AreEqual(new Size(800 / scalingFactor, 600 / scalingFactor), buffer.Current.GetMaxSize(scalingFactor));
            Assert.AreEqual(new Size(32 / scalingFactor, 32 / scalingFactor), buffer.Next.GetMaxSize(scalingFactor));

            Assert.IsTrue(buffer.Swap());
            Assert.AreSame(smallFrame, buffer.Current);
            Assert.AreSame(smallSource, buffer.Current.Source);
            Assert.AreEqual(new Size(32 / scalingFactor, 32 / scalingFactor), buffer.Current.GetMaxSize(scalingFactor));
            Assert.IsNull(buffer.Next);
            Assert.IsFalse(buffer.Swap());
        }

        [TestMethod]
        public void Frames_DpiChanges_RecalculateEachFramesOwnBounds()
        {
            var buffer = new ImagePreviewBuffer();
            buffer.Prepare(new ImagePreviewFrame(CreateSource(), new Size(800, 600)));
            buffer.Swap();
            buffer.Prepare(new ImagePreviewFrame(CreateSource(), new Size(32, 32)));

            Assert.IsNotNull(buffer.Current);
            Assert.IsNotNull(buffer.Next);
            Assert.AreEqual(new Size(800, 600), buffer.Current.GetMaxSize(1.0));
            Assert.AreEqual(new Size(400, 300), buffer.Current.GetMaxSize(2.0));
            Assert.AreEqual(new Size(16, 16), buffer.Next.GetMaxSize(2.0));
            Assert.AreEqual(new Size(800, 600), buffer.Current.PixelSize);
            Assert.AreEqual(new Size(32, 32), buffer.Next.PixelSize);
        }

        [TestMethod]
        public void Clear_RemovesBothFramesAndPendingSwap()
        {
            var buffer = new ImagePreviewBuffer();
            buffer.Prepare(new ImagePreviewFrame(CreateSource(), new Size(800, 600)));
            buffer.Swap();
            buffer.Prepare(new ImagePreviewFrame(CreateSource(), new Size(32, 32)));

            buffer.Clear();

            Assert.IsNull(buffer.Current);
            Assert.IsNull(buffer.Next);
            Assert.IsFalse(buffer.Swap());
        }
    }
}
