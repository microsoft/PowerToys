// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Folder.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Plugin.Folder.UnitTests
{
    [TestClass]
    public class EnvironmentHelperTests
    {
        [DataTestMethod]
        [DataRow(@"%", true)]
        [DataRow(@"%P", true)]
        [DataRow(@"%PROGRAMDATA%", true)]
        [DataRow(@"", false)]
        [DataRow(@"C:\ProgramData", false)]

        public void IsValidEnvironmentVariable(string search, bool expectedSuccess)
        {
            var helper = new EnvironmentHelper();

            var result = helper.IsEnvironmentVariable(search);

            Assert.AreEqual(expectedSuccess, result);
        }
    }
}
