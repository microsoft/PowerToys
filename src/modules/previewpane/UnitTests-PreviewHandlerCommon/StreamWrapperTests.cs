// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class StreamWrapperTests
    {
        [TestMethod]
        public void StreamWrapperShouldThrowIfInitializeWithNullStream()
        {
            // Arrange
            IStream stream = null;
            ArgumentNullException exception = null;

            // Act
            try
            {
                using (var streamWrapper = new ReadonlyStream(stream))
                {
                    // do work
                }
            }
            catch (ArgumentNullException ex)
            {
                exception = ex;
            }

            // Assert
            Assert.IsNotNull(exception);
        }

        [TestMethod]
        public void StreamWrapperShouldReturnCanReadTrue()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Assert
                Assert.AreEqual(true, streamWrapper.CanRead);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldReturnCanSeekTrue()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Assert
                Assert.AreEqual(true, streamWrapper.CanSeek);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldReturnCanWriteFalse()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Assert
                Assert.AreEqual(false, streamWrapper.CanWrite);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldReturnValidLength()
        {
            // Arrange
            long streamLength = 5;
            var streamMock = new Mock<IStream>();
            var stat = new System.Runtime.InteropServices.ComTypes.STATSTG
            {
                cbSize = streamLength,
            };

            streamMock.Setup(x => x.Stat(out stat, It.IsAny<int>()));

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                var actualLength = streamWrapper.Length;

                // Assert
                Assert.AreEqual(streamLength, actualLength);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldReturnValidPosition()
        {
            // Arrange
            int expectedDwOrigin = 1; // STREAM_SEEK_CUR
            long expectedOffset = 0;
            long currPosition = 5;
            var streamMock = new Mock<IStream>();

            streamMock
                .Setup(x => x.Seek(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<long, int, IntPtr>((dlibMove, dwOrigin, plibNewPosition) =>
                {
                    Marshal.WriteInt64(plibNewPosition, currPosition);
                });

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                var actualPosition = streamWrapper.Position;

                // Assert
                Assert.AreEqual(currPosition, actualPosition);
                streamMock.Verify(_ => _.Seek(It.Is<long>(offset => offset == expectedOffset), It.Is<int>(dworigin => dworigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldCallIStreamSeekWhenSetPosition()
        {
            // Arrange
            long positionToSet = 5;
            int expectedDwOrigin = 0; // STREAM_SEEK_SET
            var streamMock = new Mock<IStream>();

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                streamWrapper.Position = positionToSet;

                // Assert
                streamMock.Verify(_ => _.Seek(It.Is<long>(offset => offset == positionToSet), It.Is<int>(dworigin => dworigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
            }
        }

        [DataTestMethod]
        [DataRow(0L, SeekOrigin.Begin)]
        [DataRow(5L, SeekOrigin.Begin)]
        [DataRow(0L, SeekOrigin.Current)]
        [DataRow(5L, SeekOrigin.Current)]
        [DataRow(0L, SeekOrigin.End)]
        [DataRow(5L, SeekOrigin.End)]
        public void StreamWrapperShouldCallIStreamSeekWithValidArgumentsWhenSeekCalled(long offset, SeekOrigin origin)
        {
            // Arrange
            int expectedDwOrigin = 0;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    expectedDwOrigin = 0;
                    break;

                case SeekOrigin.Current:
                    expectedDwOrigin = 1;
                    break;

                case SeekOrigin.End:
                    expectedDwOrigin = 2;
                    break;
            }

            var streamMock = new Mock<IStream>();
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                streamWrapper.Seek(offset, origin);

                // Assert
                streamMock.Verify(_ => _.Seek(It.Is<long>(actualOffset => actualOffset == offset), It.Is<int>(actualDwOrigin => actualDwOrigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldReturnValidPositionWhenSeekCalled()
        {
            // Arrange
            long position = 5;
            var streamMock = new Mock<IStream>();

            streamMock
                .Setup(x => x.Seek(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<long, int, IntPtr>((dlibMove, dwOrigin, plibNewPosition) =>
                {
                    Marshal.WriteInt64(plibNewPosition, position);
                });

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                var actualPosition = streamWrapper.Seek(0, SeekOrigin.Begin);

                // Assert
                Assert.AreEqual(actualPosition, position);
            }
        }

        [DataTestMethod]
        [DataRow(10, -1, 5)]
        [DataRow(10, 0, -5)]
        [DataRow(10, 0, 11)]
        [DataRow(10, 5, 6)]
        public void StreamWrapperShouldThrowWhenReadCalledWithOutOfRangeArguments(int bufferLength, int offSet, int bytesToRead)
        {
            // Arrange
            var buffer = new byte[bufferLength];
            var streamMock = new Mock<IStream>();
            ArgumentOutOfRangeException exception = null;

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                try
                {
                    streamWrapper.Read(buffer, offSet, bytesToRead);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    exception = ex;
                }

                // Assert
                Assert.IsNotNull(exception);
            }
        }

        [DataTestMethod]
        [DataRow(5, 0)]
        [DataRow(5, 5)]
        [DataRow(0, 5)]
        public void StreamWrapperShouldSetValidBufferWhenReadCalled(int count, int offset)
        {
            // Arrange
            var inputBuffer = new byte[1024];
            var streamBytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                streamBytes[i] = (byte)i;
            }

            var streamMock = new Mock<IStream>();

            streamMock
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte[], int, IntPtr>((buffer, countToRead, bytesReadPtr) =>
               {
                   Array.Copy(streamBytes, 0, buffer, 0, streamBytes.Length);
                   Marshal.WriteInt32(bytesReadPtr, count);
               });

            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                // Act
                var bytesRead = streamWrapper.Read(inputBuffer, offset, count);

                // Assert
                CollectionAssert.AreEqual(streamBytes, inputBuffer.Skip(offset).Take(count).ToArray());
                Assert.AreEqual(count, bytesRead);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldThrowNotImplementedExceptionWhenFlushCalled()
        {
            // Arrange
            var streamMock = new Mock<IStream>();
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                NotImplementedException exception = null;

                // Act
                try
                {
                    streamWrapper.Flush();
                }
                catch (NotImplementedException ex)
                {
                    exception = ex;
                }

                // Assert
                Assert.IsNotNull(exception);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldThrowNotImplementedExceptionWhenSetLengthCalled()
        {
            // Arrange
            var streamMock = new Mock<IStream>();
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                NotImplementedException exception = null;

                // Act
                try
                {
                    streamWrapper.SetLength(5);
                }
                catch (NotImplementedException ex)
                {
                    exception = ex;
                }

                // Assert
                Assert.IsNotNull(exception);
            }
        }

        [TestMethod]
        public void StreamWrapperShouldThrowNotImplementedExceptionWhenWriteCalled()
        {
            // Arrange
            var streamMock = new Mock<IStream>();
            using (var streamWrapper = new ReadonlyStream(streamMock.Object))
            {
                NotImplementedException exception = null;

                // Act
                try
                {
                    streamWrapper.Write(new byte[5], 0, 0);
                }
                catch (NotImplementedException ex)
                {
                    exception = ex;
                }

                // Assert
                Assert.IsNotNull(exception);
            }
        }
    }
}
