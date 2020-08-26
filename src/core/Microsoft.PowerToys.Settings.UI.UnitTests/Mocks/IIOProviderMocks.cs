using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Moq;
using System;
using System.Collections.Generic;
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
            mockIOProvider.Setup(x => x.ReadAllText(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(() => saveContent);

            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(true);
            mockIOProvider.Setup(x => x.FileExists(It.Is<string>(x => !x.Equals(savePath, StringComparison.Ordinal))))
                          .Returns(false);

            return mockIOProvider;
        }
    }
}
