// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class FileBasedPreviewHandlerTests
    {
        internal class TestFileBasedPreviewHandler : FileBasedPreviewHandler
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
        public void FileBasedPreviewHandlerShouldSetFilePathWhenInitializeCalled(uint grfMode)
        {
            // Arrange
            var fileBasedPreviewHandler = new TestFileBasedPreviewHandler();
            var filePath = "C:\\valid-path";

            // Act
            fileBasedPreviewHandler.Initialize(filePath, grfMode);

            // Assert
            Assert.AreEqual(filePath, fileBasedPreviewHandler.FilePath);
        }
    }
}
