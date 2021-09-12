// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices.ComTypes;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class StreamBasedPreviewHandlerTests
    {
        internal class TestStreamBasedPreviewHandler : StreamBasedPreviewHandler
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
        [DataRow(0U)]
        [DataRow(1U)]
        public void StreamBasedPreviewHandlerShouldSetStreamWhenInitializeCalled(uint grfMode)
        {
            // Arrange
            var streamBasedPreviewHandler = new TestStreamBasedPreviewHandler();
            var stream = new Mock<IStream>().Object;

            // Act
            streamBasedPreviewHandler.Initialize(stream, grfMode);

            // Assert
            Assert.AreEqual(stream, streamBasedPreviewHandler.Stream);
        }
    }
}
