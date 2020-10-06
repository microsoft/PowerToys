using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            Assert.Fail("Remove");
            string savePath = string.Empty;
            string saveContent = string.Empty;
            var mockIOProvider = new Mock<IIOProvider>();

            return mockIOProvider;
        }
    }
}
