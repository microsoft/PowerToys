// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices.ComTypes;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTests_PreviewHandlerCommon
{
    [TestClass]
    public class StreamBasedPreviewHandlerTests
    {
        public class TestStreamBasedPreviewHandler : StreamBasedPreviewHandler
        {
            public override void DoPreview()
            {
                throw new NotImplementedException();
            }

            protected override IPreviewHandlerControl CreatePreviewHandlerControl()
            {
                return new Mock<IPreviewHandlerControl>().Object;
            }
        }

        [DataTestMethod]
        [DataRow((uint)0)]
        [DataRow((uint)1)]
        public void StreamBasedPreviewHandler_ShouldSetStream_WhenInitializeCalled(uint grfMode)
        {
            // Arrange
            var streamBasedPreviewHandler = new TestStreamBasedPreviewHandler();
            var stream = new Mock<IStream>().Object;

            // Act
            streamBasedPreviewHandler.Initialize(stream, grfMode);

            // Assert
            Assert.AreEqual(streamBasedPreviewHandler.Stream, stream);
        }
    }
}
