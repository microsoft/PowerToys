using Castle.Core.Logging;
using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace UnitTests_PreviewHandlerCommon
{
    [TestClass]
    public class StreamWrapperTests
    {
        [TestMethod]
        public void StreamWrapper_ShouldThrow_IfInitializeWithNullStream() 
        {
            // Arrange
            IStream stream = null;
            ArgumentNullException exception = null;

            // Act
            try
            {
                var streamWrapper = new StreamWrapper(stream);
            }
            catch (ArgumentNullException ex) 
            {
                exception = ex;
            }

            // Assert
            Assert.IsNotNull(exception);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnCanReadTrue()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            var streamWrapper = new StreamWrapper(streamMock.Object);

            // Assert
            Assert.AreEqual(streamWrapper.CanRead, true);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnCanSeekTrue()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            var streamWrapper = new StreamWrapper(streamMock.Object);

            // Assert
            Assert.AreEqual(streamWrapper.CanSeek, true);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnCanWriteFalse()
        {
            // Arrange
            var streamMock = new Mock<IStream>();

            // Act
            var streamWrapper = new StreamWrapper(streamMock.Object);

            // Assert
            Assert.AreEqual(streamWrapper.CanWrite, false);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnValidLength()
        {
            // Arrange
            long streamLength = 5;
            var stremMock = new Mock<IStream>();
            var stat = new System.Runtime.InteropServices.ComTypes.STATSTG();
            stat.cbSize = streamLength;

            stremMock
                .Setup(x => x.Stat(out stat, It.IsAny<int>()));
            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            var actualLength = streamWrapper.Length;

            // Assert
            Assert.AreEqual(actualLength, streamLength);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnValidPosition()
        {
            // Arrange
            int expectedDwOrigin = 1; // STREAM_SEEK_CUR
            long expectedOffset = 0;
            long currPosition = 5;
            var stremMock = new Mock<IStream>();

            stremMock
                .Setup(x => x.Seek(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<long, int, IntPtr>((dlibMove, dwOrigin, plibNewPosition) =>
                {
                    Marshal.WriteInt64(plibNewPosition, currPosition);
                });
            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            var actualPosition = streamWrapper.Position;

            // Assert
            Assert.AreEqual(actualPosition, currPosition);
            stremMock.Verify(_ => _.Seek(It.Is<long>(offset => offset == expectedOffset), It.Is<int>(dworigin => dworigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
        }

        [TestMethod]
        public void StreamWrapper_ShouldCallIStreamSeek_WhenSetPosition()
        {
            // Arrange
            long positionToSet = 5;
            int expectedDwOrigin = 0; // STREAM_SEEK_SET
            var stremMock = new Mock<IStream>();

            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            streamWrapper.Position = positionToSet;

            // Assert
            stremMock.Verify(_ => _.Seek(It.Is<long>(offset => offset == positionToSet), It.Is<int>(dworigin => dworigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
        }

        [DataTestMethod]
        [DataRow((long)0, SeekOrigin.Begin)]
        [DataRow((long)5, SeekOrigin.Begin)]
        [DataRow((long)0, SeekOrigin.Current)]
        [DataRow((long)5, SeekOrigin.Current)]
        [DataRow((long)0, SeekOrigin.End)]
        [DataRow((long)5, SeekOrigin.End)]
        public void StreamWrapper_ShouldCallIStreamSeekWithValidArguments_WhenSeekCalled(long offset, SeekOrigin origin)
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

            var stremMock = new Mock<IStream>();
            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            streamWrapper.Seek(offset, origin);

            // Assert
            stremMock.Verify(_ => _.Seek(It.Is<long>(actualOffset => actualOffset == offset), It.Is<int>(actualDwOrigin => actualDwOrigin == expectedDwOrigin), It.IsAny<IntPtr>()), Times.Once);
        }

        [TestMethod]
        public void StreamWrapper_ShouldReturnValidPosition_WhenSeekCalled()
        {
            // Arrange
            long position = 5;
            var stremMock = new Mock<IStream>();

            stremMock
                .Setup(x => x.Seek(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<long, int, IntPtr>((dlibMove, dwOrigin, plibNewPosition) =>
                {
                    Marshal.WriteInt64(plibNewPosition, position);
                });

            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            var actualPosition = streamWrapper.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.AreEqual(position, actualPosition);
        }

        [DataTestMethod]
        [DataRow(10, -1, 5)]
        [DataRow(10, 0, -5)]
        [DataRow(10, 0, 11)]
        [DataRow(10, 5, 6)]
        public void StreamWrapper_ShouldThrow_WhenReadCalledWithOutOfRangeArguments(int bufferLength, int offSet, int bytesToRead)
        {
            // Arrange
            var buffer = new byte[bufferLength];
            var stremMock = new Mock<IStream>();
            ArgumentOutOfRangeException exception = null;

            var streamWrapper = new StreamWrapper(stremMock.Object);

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

        [DataTestMethod]
        [DataRow(5, 0)]
        [DataRow(5, 5)]
        [DataRow(0, 5)]
        public void StreamWrapper_ShouldSetValidBuffer_WhenReadCalled(int count, int offset)
        {
            // Arrange
            var inputBuffer = new byte[1024];
            var streamBytes = new byte[count];
            for (int i = 0; i < count; i++) 
            {
                streamBytes[i] = (byte)i;
            }

            var stremMock = new Mock<IStream>();

            stremMock
                .Setup(x => x.Read(It.IsAny<byte []>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte [], int, IntPtr>((buffer, countToRead , bytesReadPtr) =>
                {
                    Array.Copy(streamBytes, 0, buffer, 0, streamBytes.Length);
                    Marshal.WriteInt32(bytesReadPtr, count);
                });

            var streamWrapper = new StreamWrapper(stremMock.Object);

            // Act
            var bytesRead = streamWrapper.Read(inputBuffer, offset, count);

            // Assert
            CollectionAssert.AreEqual(streamBytes, inputBuffer.Skip(offset).Take(count).ToArray());
            Assert.AreEqual(count, bytesRead);
        }

        [TestMethod]
        public void StreamWrapper_ShouldThrowNotImplementedException_WhenFlushCalled()
        {
            // Arrange
            var stremMock = new Mock<IStream>();
            var streamWrapper = new StreamWrapper(stremMock.Object);
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

        [TestMethod]
        public void StreamWrapper_ShouldThrowNotImplementedException_WhenSetLengthCalled()
        {
            // Arrange
            var stremMock = new Mock<IStream>();
            var streamWrapper = new StreamWrapper(stremMock.Object);
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

        [TestMethod]
        public void StreamWrapper_ShouldThrowNotImplementedException_WhenWriteCalled()
        {
            // Arrange
            var stremMock = new Mock<IStream>();
            var streamWrapper = new StreamWrapper(stremMock.Object);
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
