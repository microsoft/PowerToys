using Moq;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq.Expressions;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class IIOProviderMocks
    {
        /// <summary>
        /// This method mocks an IO provider so that it will always return data at the savePath location. 
        /// This mock is specific to a given module, and is verifiable that the stub file was read.
        /// </summary>
        /// <param name="savePath">The path to the stub settings file</param>
        /// <param name="expectedPathSubstring">The substring in the path that identifies the module eg. Microsoft\\PowerToys\\ColorPicker</param>
        /// <returns></returns>
        internal static Mock<IFile> GetMockIOReadWithStubFile(string savePath, Expression<Func<string, bool>> filterExpression)
        {
            string saveContent = File.ReadAllText(savePath);
            var fileMock = new Mock<IFile>();


            fileMock.Setup(x => x.ReadAllText(It.Is<string>(filterExpression)))
                         .Returns(() => saveContent).Verifiable();

            
            fileMock.Setup(x => x.Exists(It.Is<string>(filterExpression)))
                          .Returns(true);

            return fileMock;
        }

        internal static void VerifyIOReadWithStubFile(Mock<IFile> fileMock, Expression<Func<string, bool>> filterExpression, int expectedCallCount)
        {
            fileMock.Verify(x => x.ReadAllText(It.Is<string>(filterExpression)), Times.Exactly(expectedCallCount));
        }
    }
}
