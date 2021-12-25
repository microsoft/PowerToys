// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Linq.Expressions;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Moq;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class IIOProviderMocks
    {
        /// <summary>
        /// This method mocks an IO provider to validate tests which required saving to a file, and then reading the contents of that file, or verifying it exists
        /// </summary>
        /// <returns>Mocked IO Provider</returns>
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

        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;

        /// <summary>
        /// This method mocks an IO provider so that it will always return data at the savePath location.
        /// This mock is specific to a given module, and is verifiable that the stub file was read.
        /// </summary>
        /// <param name="savePath">The path to the stub settings file</param>
        /// <param name="filterExpression">The substring in the path that identifies the module eg. Microsoft\\PowerToys\\ColorPicker</param>
        /// <returns>Mocked IFile</returns>
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
