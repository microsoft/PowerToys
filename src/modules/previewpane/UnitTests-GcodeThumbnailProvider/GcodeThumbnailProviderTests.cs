// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Common.ComInterlop;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.PowerToys.ThumbnailHandler.Gcode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GcodeThumbnailProviderUnitTests
{
    [STATestClass]
    public class GcodeThumbnailProviderTests
    {
        [TestMethod]
        public void GetThumbnailValidStreamGcode()
        {
            // Act
            var file = File.ReadAllBytes("HelperFiles/sample.gcode");

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider();

            provider.Initialize(GetMockStream(file), 0);

            provider.GetThumbnail(256, out IntPtr bitmap, out WTS_ALPHATYPE alphaType);

            Assert.IsTrue(bitmap != IntPtr.Zero);
            Assert.IsTrue(alphaType == WTS_ALPHATYPE.WTSAT_ARGB);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeGcode()
        {
            // Act
            var file = File.ReadAllBytes("HelperFiles/sample.gcode");

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider();

            provider.Initialize(GetMockStream(file), 0);

            provider.GetThumbnail(0, out IntPtr bitmap, out WTS_ALPHATYPE alphaType);

            Assert.IsTrue(bitmap == IntPtr.Zero);
            Assert.IsTrue(alphaType == WTS_ALPHATYPE.WTSAT_UNKNOWN);
        }

        [TestMethod]
        public void GetThumbnailToBigGcode()
        {
            // Act
            var file = File.ReadAllBytes("HelperFiles/sample.gcode");

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider();

            provider.Initialize(GetMockStream(file), 0);

            provider.GetThumbnail(10001, out IntPtr bitmap, out WTS_ALPHATYPE alphaType);

            Assert.IsTrue(bitmap == IntPtr.Zero);
            Assert.IsTrue(alphaType == WTS_ALPHATYPE.WTSAT_UNKNOWN);
        }

        [TestMethod]
        public void CheckNoGcodeEmptyStringShouldReturnNullBitmap()
        {
            using (var reader = new StringReader(string.Empty))
            {
                Bitmap thumbnail = GcodeThumbnailProvider.GetThumbnail(reader, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoGcodeNullStringShouldReturnNullBitmap()
        {
            Bitmap thumbnail = GcodeThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }

        private static IStream GetMockStream(byte[] sourceArray)
        {
            var streamMock = new Mock<IStream>();
            int bytesRead = 0;

            streamMock
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte[], int, IntPtr>((buffer, countToRead, bytesReadPtr) =>
                {
                    int actualCountToRead = Math.Min(sourceArray.Length - bytesRead, countToRead);
                    if (actualCountToRead > 0)
                    {
                        Array.Copy(sourceArray, bytesRead, buffer, 0, actualCountToRead);
                        Marshal.WriteInt32(bytesReadPtr, actualCountToRead);
                        bytesRead += actualCountToRead;
                    }
                    else
                    {
                        Marshal.WriteInt32(bytesReadPtr, 0);
                    }
                });

            return streamMock.Object;
        }
    }
}
