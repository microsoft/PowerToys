using System;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTests_PreviewHandlerCommon
{
    [TestClass]
    public class FileBasedPreviewHandlerTests
    {
        public class TestFileBasedPreviewHandler : FileBasedPreviewHandler
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
        public void FileBasedPreviewHandler_ShouldSetFilePath_WhenInitializeCalled(uint grfMode)
        {
            // Arrange
            var fileBasedPreviewHandler = new TestFileBasedPreviewHandler();
            var filePath = "C:\\valid-path";

            // Act
            fileBasedPreviewHandler.Initialize(filePath, grfMode);

            // Assert
            Assert.AreEqual(fileBasedPreviewHandler.FilePath, filePath);
        }
    }
}
