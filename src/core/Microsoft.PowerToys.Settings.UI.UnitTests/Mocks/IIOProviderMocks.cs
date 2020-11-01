using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class IIOProviderMocks
    {

        /// <summary>
        /// This method mocks an IO provider to validate tests wich required saving to a file, and then reading the contents of that file, or verifying it exists
        /// </summary>
        /// <returns></returns>
        internal static Mock<IIOProvider> GetMockIOProviderForSaveLoadExists()
        {
            string savePath = string.Empty;
            string saveContent = string.Empty;
            var mockIOProvider = new Mock<IIOProvider>();
            mockIOProvider.Setup(x => x.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                          .Callback<string, string>((path, content) =>
                          {
                              savePath = path;
                              saveContent = content;
                          });
            // Using Ordinal since this is used internally for a path
            mockIOProvider.Setup(x => x.ReadAllText(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(() => saveContent);
            // Using Ordinal since this is used internally for a path
            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(true);
            // Using Ordinal since this is used internally for a path
            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => !x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(false);

            return mockIOProvider;
        }



        /// <summary>
        /// This method mocks an IO provider so that it will always return data at the savePath location. 
        /// This mock is specific to a given module, and is verifiable that the stub file was read.
        /// </summary>
        /// <param name="savePath">The path to the stub settings file</param>
        /// <param name="expectedPathSubstring">The substring in the path that identifies the module eg. Microsoft\\PowerToys\\ColorPicker</param>
        /// <returns></returns>
        internal static Mock<IIOProvider> GetMockIOReadWithStubFile(string savePath, Expression<Func<string, bool>> filterExpression)
        {
            string saveContent = File.ReadAllText(savePath);
            var mockIOProvider = new Mock<IIOProvider>();


            mockIOProvider.Setup(x => x.ReadAllText(It.Is<string>(filterExpression)))
                         .Returns(() => saveContent).Verifiable();

            
            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(filterExpression)))
                          .Returns(true);

            return mockIOProvider;
        }

        internal static void VerifyIOReadWithStubFile(Mock<IIOProvider> mockIOProvider, Expression<Func<string, bool>> filterExpression, int expectedCallCount)
        {
            mockIOProvider.Verify(x => x.ReadAllText(It.Is<string>(filterExpression)), Times.Exactly(expectedCallCount));
        }
    }
}
